"""The only keys a log line of the service may carry."""

from enum import StrEnum


class LogField(StrEnum):
    """A key allowed on a log line: pointers and counts, never content."""

    REFERENCE = "reference"
    CHARS = "chars"
    ELAPSED_MS = "elapsed_ms"
    MODEL_VERSION = "model_version"
    COUNT_BY_KIND = "count_by_kind"
    STEP = "step"
    OUTCOME = "outcome"
