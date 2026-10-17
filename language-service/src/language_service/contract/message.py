"""The base of every message of the border, and the one place its validation mode is set."""

from pydantic import BaseModel, ConfigDict


class Message(BaseModel):
    """Base of every message: no extra field, no coercion, immutable."""

    model_config = ConfigDict(extra="forbid", strict=True, frozen=True)
