"""The gate at the HTTP border: every request needs the startup secret, checked before any body."""

import logging
from collections.abc import Iterable
from typing import Final

from starlette.responses import JSONResponse
from starlette.types import ASGIApp, Receive, Scope, Send

from language_service.auth import SecretVerifier

SECRET_HEADER: Final = b"x-service-secret"
HTTP_UNAUTHORIZED: Final = 401
REFUSAL_MESSAGE: Final = "pedido recusado: segredo de inicialização ausente ou incorreto"

LOGGER = logging.getLogger("language_service")


class SecretGate:
    """Refuses every HTTP request without the secret, before the body is read and before routing.

    The refusal is logged as one fixed line: no header, no path, no body.
    """

    def __init__(self, app: ASGIApp, verifier: SecretVerifier) -> None:
        self._app = app
        self._verifier = verifier

    async def __call__(self, scope: Scope, receive: Receive, send: Send) -> None:
        if scope["type"] != "http":
            await self._app(scope, receive, send)
            return
        if self._verifier.accepts(secret_of(scope)):
            await self._app(scope, receive, send)
            return
        LOGGER.warning(REFUSAL_MESSAGE)
        refusal = JSONResponse({"error": "unauthorized"}, status_code=HTTP_UNAUTHORIZED)
        await refusal(scope, receive, send)


def secret_of(scope: Scope) -> bytes | None:
    """The value of the secret header, or None when the request carries none."""
    headers: Iterable[tuple[bytes, bytes]] = scope["headers"]
    return next((value for name, value in headers if name == SECRET_HEADER), None)
