"""What each route answers. Registering them is the composition root's job, not this module's."""

from language_service import __version__
from language_service.contract import CONTRACT_VERSION, HealthResponse, VersionResponse


def health() -> HealthResponse:
    return HealthResponse(status="ok")


def version() -> VersionResponse:
    return VersionResponse(service_version=__version__, contract_version=CONTRACT_VERSION)
