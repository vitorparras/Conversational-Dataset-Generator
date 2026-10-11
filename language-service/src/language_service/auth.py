"""Authentication by the secret of the startup, compared in constant time.

The secret is read once from standard input. This module keeps only its SHA-256 digest: comparing
digests takes the same time whatever the candidate's length, and the raw secret is not retained.
"""

import hashlib
import hmac
import secrets
from typing import Final

from starlette.types import ASGIApp, Receive, Scope, Send

from language_service.logging_ import ServiceLog

SECRET_HEADER: Final = b"x-service-secret"


class SecretVerifier:
    """Accepts a candidate only when it equals the secret the service started with."""

    def __init__(self, secret: bytes) -> None:
        if not secret:
            raise ValueError("the secret must not be empty")
        self._digest = hashlib.sha256(secret).digest()

    def accepts(self, candidate: bytes | None) -> bool:
        """Whether ``candidate`` is the secret; a missing candidate is never accepted."""
        if candidate is None:
            return False
        return hmac.compare_digest(self._digest, hashlib.sha256(candidate).digest())


def new_reference() -> str:
    """A fresh opaque reference, for events that happen before any reference is known."""
    return secrets.token_hex(8)


async def send_json(send: Send, status: int, body: bytes) -> None:
    """Send a complete JSON response."""
    await send(
        {
            "type": "http.response.start",
            "status": status,
            "headers": [
                (b"content-type", b"application/json"),
                (b"content-length", str(len(body)).encode("ascii")),
            ],
        }
    )
    await send({"type": "http.response.body", "body": body})


class AuthMiddleware:
    """Refuses every request without the secret, before the body is read and before routing."""

    def __init__(self, app: ASGIApp, verifier: SecretVerifier, log: ServiceLog) -> None:
        self._app = app
        self._verifier = verifier
        self._log = log

    async def __call__(self, scope: Scope, receive: Receive, send: Send) -> None:
        if scope["type"] == "lifespan":
            await self._app(scope, receive, send)
            return
        if scope["type"] == "http":
            candidate = next((value for name, value in scope["headers"] if name == SECRET_HEADER), None)
            if self._verifier.accepts(candidate):
                await self._app(scope, receive, send)
                return
            reference = new_reference()
            self._log.event(reference=reference, step="auth", outcome="refused")
            body = b'{"error":"unauthorized","reference":"' + reference.encode("ascii") + b'"}'
            await send_json(send, 401, body)
            return
        # Any other scope (a WebSocket, say) has no use here and is not served.
        if scope["type"] == "websocket":
            await send({"type": "websocket.close", "code": 1008})
