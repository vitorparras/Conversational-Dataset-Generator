"""The service log: one JSON line per event, with a closed set of fields.

There is no free-text message. A line carries the fields below and nothing else, so there is no
place to put the text, a piece of it or an offset (requirement 021, decision 0011).
"""

import json
import threading
from collections.abc import Sequence
from typing import Final, TextIO

ALLOWED_FIELDS: Final = frozenset(
    {"reference", "chars", "elapsed_ms", "model_version", "count_by_kind", "step", "outcome"}
)


class ForbiddenLogFieldError(ValueError):
    """A line asked for a field outside the closed set. The name is the code's, never the text's."""


class ServiceLog:
    """Writes events to every sink, one line each."""

    def __init__(self, sinks: Sequence[TextIO]) -> None:
        self._sinks = tuple(sinks)
        self._lock = threading.Lock()

    def event(self, **fields: object) -> None:
        """Write one line with ``fields``; any field outside ``ALLOWED_FIELDS`` is refused."""
        unknown = sorted(set(fields) - ALLOWED_FIELDS)
        if unknown:
            raise ForbiddenLogFieldError(f"fields outside the closed set: {', '.join(unknown)}")
        line = json.dumps(fields, sort_keys=True, ensure_ascii=True, allow_nan=False)
        with self._lock:
            for sink in self._sinks:
                sink.write(line + "\n")
                sink.flush()
