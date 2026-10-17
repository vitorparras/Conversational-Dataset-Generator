"""The composition root: the only module that builds the application and registers its routes.

Nothing here is read from the environment, and there is no documentation route and no switch of
any kind: a surface that does not exist cannot be turned on.
"""

import logging

from fastapi import FastAPI
from fastapi.exceptions import RequestValidationError

from language_service import __version__
from language_service.auth import SecretVerifier
from language_service.contract import AnalyzeResponse, HealthResponse, VersionResponse
from language_service.gate import SecretGate
from language_service.logging_ import RequestLog
from language_service.recognizers.empty import EmptyRecognizer
from language_service.routes import Routes, refuse_invalid_request


def create_app(secret: bytes) -> FastAPI:
    """The application that accepts only requests carrying ``secret``."""
    routes = Routes(EmptyRecognizer(), RequestLog(logging.getLogger("language_service")))
    app = FastAPI(
        title="language_service",
        version=__version__,
        docs_url=None,
        redoc_url=None,
        openapi_url=None,
    )
    app.add_exception_handler(RequestValidationError, refuse_invalid_request)
    app.add_api_route("/health", routes.health, methods=["GET"], response_model=HealthResponse)
    app.add_api_route("/version", routes.version, methods=["GET"], response_model=VersionResponse)
    app.add_api_route("/analyze", routes.analyze, methods=["POST"], response_model=AnalyzeResponse)
    app.add_middleware(SecretGate, verifier=SecretVerifier.from_secret(secret))
    return app
