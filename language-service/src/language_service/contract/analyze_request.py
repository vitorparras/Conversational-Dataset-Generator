"""The request of the analysis route."""

from typing import Final

from pydantic import Field

from language_service.contract.message import Message

REFERENCE_PATTERN: Final = r"^[A-Za-z0-9_-]{1,64}$"


class AnalyzeRequest(Message):
    """What the core sends: the text, its language, an opaque reference and the contract version."""

    text: str
    language: str
    reference: str = Field(pattern=REFERENCE_PATTERN)
    contract_version: str
