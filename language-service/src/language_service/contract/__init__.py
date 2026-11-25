"""The messages of the HTTP border, as pydantic models: the only place a schema lives.

Offsets are code points, which is what Python indexes by; the core converts to its own unit. An
unknown field in either direction is an error, never something to ignore.
"""

from typing import Final

from language_service.contract.analyze_item_request import AnalyzeItemRequest
from language_service.contract.analyze_item_response import AnalyzeItemResponse
from language_service.contract.analyze_request import AnalyzeRequest
from language_service.contract.analyze_response import AnalyzeResponse
from language_service.contract.author_group import AuthorGroup
from language_service.contract.grouped_occurrence import GroupedOccurrence
from language_service.contract.health_response import HealthResponse
from language_service.contract.item_text import ItemText
from language_service.contract.version_response import VersionResponse

CONTRACT_VERSION: Final = "2"

__all__ = [
    "CONTRACT_VERSION",
    "AnalyzeItemRequest",
    "AnalyzeItemResponse",
    "AnalyzeRequest",
    "AnalyzeResponse",
    "AuthorGroup",
    "GroupedOccurrence",
    "HealthResponse",
    "ItemText",
    "VersionResponse",
]
