"""The request of the item analysis route."""

from pydantic import Field

from language_service.contract.analyze_request import REFERENCE_PATTERN
from language_service.contract.item_text import ItemText
from language_service.contract.message import Message


class AnalyzeItemRequest(Message):
    """What the core sends for an item: its texts in order, language, reference and version."""

    texts: list[ItemText]
    language: str
    reference: str = Field(pattern=REFERENCE_PATTERN)
    contract_version: str
