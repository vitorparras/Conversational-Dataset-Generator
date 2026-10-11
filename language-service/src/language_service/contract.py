"""The boundary contract of decision 0018, as strict pydantic models.

Offsets are measured in code points, which is what Python indexes by; the core converts to its own
unit. There is no confidence field: decision 0018 removed it, and an unknown field in either
direction is an error rather than something to ignore.
"""

from typing import Final, Literal

from pydantic import BaseModel, ConfigDict, Field

CONTRACT_VERSION: Final = "1"

# Patterns shared with the log: whatever the contract lets through is safe to write as a log value.
REFERENCE_PATTERN: Final = r"^[A-Za-z0-9_-]{1,64}$"
KIND_PATTERN: Final = r"^[A-Za-z][A-Za-z0-9_.-]{0,31}$"
LABEL_PATTERN: Final = r"^[A-Za-z][A-Za-z0-9_.:-]{0,31}$"
MODEL_VERSION_PATTERN: Final = r"^[A-Za-z0-9._+-]{1,64}$"
LANGUAGE_PATTERN: Final = r"^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8}){0,3}$"
CONTRACT_VERSION_PATTERN: Final = r"^[A-Za-z0-9._-]{1,16}$"

MAX_TEXT_CHARS: Final = 1_000_000


class _Strict(BaseModel):
    """Base of every message: no extra field, no coercion, immutable."""

    model_config = ConfigDict(extra="forbid", strict=True, frozen=True)


class AnalyzeRequest(_Strict):
    """What the core sends: the text, its language, an opaque reference and the contract version."""

    text: str = Field(max_length=MAX_TEXT_CHARS)
    language: str = Field(pattern=LANGUAGE_PATTERN)
    reference: str = Field(pattern=REFERENCE_PATTERN)
    contract_version: str = Field(pattern=CONTRACT_VERSION_PATTERN)


class Occurrence(_Strict):
    """One place in the text, as code point offsets with half-open bounds, and what is known of it."""

    start: int = Field(ge=0)
    end: int = Field(ge=0)
    kind: str = Field(pattern=KIND_PATTERN)
    part_of_speech: str = Field(pattern=LABEL_PATTERN)
    syntactic_role: str = Field(pattern=LABEL_PATTERN)


class AnalyzeResponse(_Strict):
    """An empty list means analysed and nothing found; an unavailable service never answers this."""

    occurrences: list[Occurrence]
    model_version: str | None = Field(pattern=MODEL_VERSION_PATTERN)


class HealthResponse(_Strict):
    """The service is up and the secret was accepted."""

    status: Literal["ok"]
    model_version: str | None = Field(pattern=MODEL_VERSION_PATTERN)


class VersionResponse(_Strict):
    """Which service, which contract, which model. No model is an explicit null."""

    service_version: str
    contract_version: str
    model_version: str | None = Field(pattern=MODEL_VERSION_PATTERN)
