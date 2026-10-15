"""The messages of the HTTP border, as pydantic models: the only place a schema lives.

Offsets are code points, which is what Python indexes by; the core converts to its own unit. An
unknown field in either direction is an error, never something to ignore.
"""

from typing import Final

from language_service.contract.analyze_request import AnalyzeRequest
from language_service.contract.analyze_response import AnalyzeResponse
from language_service.contract.health_response import HealthResponse
from language_service.contract.version_response import VersionResponse

CONTRACT_VERSION: Final = "1"

__all__ = [
    "CONTRACT_VERSION",
    "AnalyzeRequest",
    "AnalyzeResponse",
    "HealthResponse",
    "VersionResponse",
]
