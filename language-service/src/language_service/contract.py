"""The messages of the HTTP border, as pydantic models: the only place a schema lives.

An unknown field in either direction is an error, never something to ignore (decision 0018).
"""

from typing import Final, Literal

from pydantic import BaseModel, ConfigDict

CONTRACT_VERSION: Final = "1"


class Message(BaseModel):
    """Base of every message: no extra field, no coercion, immutable."""

    model_config = ConfigDict(extra="forbid", strict=True, frozen=True)


class HealthResponse(Message):
    """The service is up and the secret was accepted."""

    status: Literal["ok"]


class VersionResponse(Message):
    """Which service and which contract."""

    service_version: str
    contract_version: str
