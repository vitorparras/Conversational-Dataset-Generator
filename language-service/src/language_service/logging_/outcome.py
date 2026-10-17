"""How a request ended."""

from enum import StrEnum


class Outcome(StrEnum):
    """The two ways an analysis ends."""

    OK = "ok"
    FAILED = "failed"
