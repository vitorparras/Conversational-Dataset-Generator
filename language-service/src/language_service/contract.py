"""The messages of the HTTP border, as pydantic models: the only place a schema lives.

Offsets are code points, which is what Python indexes by; the core converts to its own unit. An
unknown field in either direction is an error, never something to ignore.
"""

from typing import Final, Literal

from pydantic import BaseModel, ConfigDict

from language_service.recognizers.base import Occurrence

CONTRACT_VERSION: Final = "1"


class Message(BaseModel):
    """Base of every message: no extra field, no coercion, immutable."""

    model_config = ConfigDict(extra="forbid", strict=True, frozen=True)


class AnalyzeRequest(Message):
    """What the core sends: the text, its language, an opaque reference and the contract version."""

    text: str
    language: str
    reference: str
    contract_version: str


class AnalyzeResponse(Message):
    """An empty list means analysed and nothing found; an unavailable service never answers."""

    occurrences: tuple[Occurrence, ...]
    model_version: str | None


class HealthResponse(Message):
    """The service is up and the secret was accepted."""

    status: Literal["ok"]


class VersionResponse(Message):
    """Which service, which contract, which model; no model is an explicit null."""

    service_version: str
    contract_version: str
    model_version: str | None
