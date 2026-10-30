"""The gate at the HTTP border: every request needs the startup secret, checked before any body.

The version route is the one exception: it asks for no secret, and answers a nonce with the proof
that this process holds the secret, so the core knows who owns the port before it sends the secret.
"""

import logging
import re
from collections.abc import Iterable
from typing import Final

from starlette.responses import JSONResponse
from starlette.types import ASGIApp, Message, Receive, Scope, Send

from language_service.auth import SecretVerifier
from language_service.proof import SecretProver

SECRET_HEADER: Final = b"x-service-secret"
NONCE_HEADER: Final = b"x-service-nonce"
PROOF_HEADER: Final = b"x-service-proof"
NONCE_SHAPE: Final = re.compile(rb"[0-9a-f]{32,128}")
VERSION_ROUTE: Final = ("GET", "/version")
HTTP_OK: Final = 200
HTTP_BAD_REQUEST: Final = 400
HTTP_UNAUTHORIZED: Final = 401
REFUSAL_MESSAGE: Final = "pedido recusado: segredo de inicialização ausente ou incorreto"

LOGGER = logging.getLogger("language_service")


class SecretGate:
    """Refuses every request without the secret before reading its body, logging one fixed line."""

    def __init__(self, app: ASGIApp, verifier: SecretVerifier, prover: SecretProver) -> None:
        self._app = app
        self._verifier = verifier
        self._prover = prover

    async def __call__(self, scope: Scope, receive: Receive, send: Send) -> None:
        if scope["type"] != "http":
            await self._app(scope, receive, send)
            return
        if (scope["method"], scope["path"]) == VERSION_ROUTE:
            await self._prove(scope, receive, send)
            return
        if self._verifier.accepts(header_of(scope, SECRET_HEADER)):
            await self._app(scope, receive, send)
            return
        LOGGER.warning(REFUSAL_MESSAGE)
        refusal = JSONResponse({"error": "unauthorized"}, status_code=HTTP_UNAUTHORIZED)
        await refusal(scope, receive, send)

    async def _prove(self, scope: Scope, receive: Receive, send: Send) -> None:
        """Answer the version route; with a well-formed nonce, the answer carries the proof."""
        nonce = header_of(scope, NONCE_HEADER)
        if nonce is None:
            await self._app(scope, receive, send)
        elif NONCE_SHAPE.fullmatch(nonce):
            await self._app(scope, receive, with_proof(send, self._prover.proof(nonce)))
        else:
            refusal = JSONResponse({"error": "invalid_request"}, status_code=HTTP_BAD_REQUEST)
            await refusal(scope, receive, send)


def header_of(scope: Scope, name: bytes) -> bytes | None:
    """The value of the header ``name``, or None when the request carries none."""
    headers: Iterable[tuple[bytes, bytes]] = scope["headers"]
    return next((value for key, value in headers if key == name), None)


def with_proof(send: Send, proof: bytes) -> Send:
    """``send`` that adds the proof header to a successful answer and changes nothing else."""

    async def sending(message: Message) -> None:
        if message["type"] == "http.response.start" and message["status"] == HTTP_OK:
            message = {**message, "headers": [*message["headers"], (PROOF_HEADER, proof)]}
        await send(message)

    return sending
