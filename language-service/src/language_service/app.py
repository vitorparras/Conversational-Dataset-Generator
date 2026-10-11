"""The HTTP application: ``/health``, ``/version`` and ``/analyze``, and nothing else.

Every request needs the startup secret. No route, flag or mode prints a request body: the only
output is the closed-field log of ``logging_``.
"""

import time
from collections import Counter

from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from starlette.types import ASGIApp, Receive, Scope, Send

from language_service import __version__
from language_service.auth import AuthMiddleware, SecretVerifier, new_reference, send_json
from language_service.contract import (
    CONTRACT_VERSION,
    AnalyzeRequest,
    AnalyzeResponse,
    HealthResponse,
    VersionResponse,
)
from language_service.logging_ import ServiceLog
from language_service.recognizers.base import Recognizer


class FailureBoundary:
    """Catches what no handler caught and answers with the reference and the step, never the cause.

    The exception message may hold the text being analysed, so only the type name is logged.
    """

    def __init__(self, app: ASGIApp, log: ServiceLog) -> None:
        self._app = app
        self._log = log

    async def __call__(self, scope: Scope, receive: Receive, send: Send) -> None:
        if scope["type"] != "http":
            await self._app(scope, receive, send)
            return
        state = scope.setdefault("state", {})
        try:
            await self._app(scope, receive, send)
        except Exception as error:  # noqa: BLE001 - the last line: log the type, never the message
            reference = state.get("reference") or new_reference()
            step = state.get("step") or "serve"
            self._log.event(reference=reference, step=step, outcome=f"error:{type(error).__name__}")
            body = b'{"error":"internal_error","reference":"' + reference.encode("ascii") + b'"}'
            await send_json(send, 500, body)


def create_app(*, verifier: SecretVerifier, recognizer: Recognizer, log: ServiceLog) -> FastAPI:
    """Build the application around its collaborators; nothing is read from the environment."""
    # The generated documentation routes would be extra surface: there is none.
    app = FastAPI(
        title="language_service",
        version=__version__,
        docs_url=None,
        redoc_url=None,
        openapi_url=None,
    )

    @app.exception_handler(RequestValidationError)
    async def refuse_invalid(request: Request, error: RequestValidationError) -> JSONResponse:
        # The default answer echoes the input, which is the text: it is dropped here.
        reference = new_reference()
        log.event(reference=reference, step="validate", outcome="refused")
        return JSONResponse(
            status_code=400,
            content={"error": "invalid_request", "reference": reference},
        )

    @app.get("/health", response_model=HealthResponse)
    def health() -> HealthResponse:
        return HealthResponse(status="ok", model_version=recognizer.model_version)

    @app.get("/version", response_model=VersionResponse)
    def version() -> VersionResponse:
        return VersionResponse(
            service_version=__version__,
            contract_version=CONTRACT_VERSION,
            model_version=recognizer.model_version,
        )

    @app.post("/analyze", response_model=AnalyzeResponse)
    def analyze(request: Request, body: AnalyzeRequest) -> AnalyzeResponse | JSONResponse:
        started = time.perf_counter()
        request.state.reference = body.reference
        request.state.step = "validate"
        if body.contract_version != CONTRACT_VERSION:
            log.event(reference=body.reference, step="validate", outcome="refused")
            return JSONResponse(
                status_code=400,
                content={
                    "error": "unsupported_contract_version",
                    "reference": body.reference,
                    "supported": CONTRACT_VERSION,
                },
            )

        request.state.step = "recognize"
        found = list(recognizer.recognize(body.text, body.language))

        request.state.step = "check"
        length = len(body.text)
        for occurrence in found:
            if not occurrence.start <= occurrence.end <= length:
                raise ValueError("occurrence outside the text")

        request.state.step = "respond"
        response = AnalyzeResponse(occurrences=found, model_version=recognizer.model_version)
        log.event(
            reference=body.reference,
            chars=length,
            elapsed_ms=round((time.perf_counter() - started) * 1000, 3),
            model_version=recognizer.model_version,
            count_by_kind=dict(Counter(occurrence.kind for occurrence in found)),
            step="respond",
            outcome="ok",
        )
        return response

    # Last added is outermost: authentication first, then the failure boundary around the routes.
    app.add_middleware(FailureBoundary, log=log)
    app.add_middleware(AuthMiddleware, verifier=verifier, log=log)
    return app
