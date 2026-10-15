"""The composition root: the only module that builds the application and registers its routes.

Nothing here is read from the environment, and there is no documentation route and no switch of
any kind: a surface that does not exist cannot be turned on.
"""

from fastapi import FastAPI

from language_service import __version__
from language_service.auth import SecretVerifier
from language_service.contract import HealthResponse, VersionResponse
from language_service.gate import SecretGate
from language_service.routes import health, version


def create_app(secret: bytes) -> FastAPI:
    """The application that accepts only requests carrying ``secret``."""
    app = FastAPI(
        title="language_service",
        version=__version__,
        docs_url=None,
        redoc_url=None,
        openapi_url=None,
    )
    app.add_api_route("/health", health, methods=["GET"], response_model=HealthResponse)
    app.add_api_route("/version", version, methods=["GET"], response_model=VersionResponse)
    app.add_middleware(SecretGate, verifier=SecretVerifier.from_secret(secret))
    return app
