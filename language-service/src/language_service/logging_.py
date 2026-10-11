"""The service log: one JSON line per event, with a closed set of fields.

There is no free-text message. A line carries the fields below and nothing else, so there is no
place to put the text, a piece of it or an offset (requirement 021, decision 0011, issue 33).

The set is closed in two ways. A field outside it is refused by name. A field inside it is refused
unless its value has the shape of that field: a reference is a short opaque token, a step is one of
a fixed list, an outcome is one of three words or an exception type name. A sentence does not fit
any of them, so writing the text under an allowed name fails too.
"""

import json
import math
import re
import threading
from collections.abc import Mapping, Sequence
from typing import Final, TextIO

from language_service.contract import KIND_PATTERN, MODEL_VERSION_PATTERN, REFERENCE_PATTERN

ALLOWED_FIELDS: Final = frozenset(
    {"reference", "chars", "elapsed_ms", "model_version", "count_by_kind", "step", "outcome"}
)

STEPS: Final = frozenset({"startup", "auth", "validate", "recognize", "check", "respond", "serve"})

_REFERENCE: Final = re.compile(REFERENCE_PATTERN)
_MODEL_VERSION: Final = re.compile(MODEL_VERSION_PATTERN)
_KIND: Final = re.compile(KIND_PATTERN)
_OUTCOME: Final = re.compile(r"^(ok|refused|error:[A-Za-z_][A-Za-z0-9_]{0,63})$")
_MAX_KINDS: Final = 64


class ForbiddenLogFieldError(ValueError):
    """A line asked for a field outside the closed set. The message names fields, never values."""


class InvalidLogValueError(ValueError):
    """A field of the set carried a value that does not have its shape. The message names the field."""


def _is_count(value: object) -> bool:
    return isinstance(value, int) and not isinstance(value, bool) and value >= 0


def _valid(name: str, value: object) -> bool:
    match name:
        case "reference":
            return isinstance(value, str) and _REFERENCE.fullmatch(value) is not None
        case "chars":
            return _is_count(value)
        case "elapsed_ms":
            return (
                isinstance(value, int | float)
                and not isinstance(value, bool)
                and math.isfinite(value)
                and value >= 0
            )
        case "model_version":
            return value is None or (isinstance(value, str) and _MODEL_VERSION.fullmatch(value) is not None)
        case "count_by_kind":
            return (
                isinstance(value, Mapping)
                and len(value) <= _MAX_KINDS
                and all(isinstance(kind, str) and _KIND.fullmatch(kind) for kind in value)
                and all(_is_count(count) for count in value.values())
            )
        case "step":
            return isinstance(value, str) and value in STEPS
        case "outcome":
            return isinstance(value, str) and _OUTCOME.fullmatch(value) is not None
        case _:
            return False


def format_event(**fields: object) -> str:
    """Return the JSON line for ``fields``, or raise if any field or value is not allowed."""
    unknown = sorted(set(fields) - ALLOWED_FIELDS)
    if unknown:
        raise ForbiddenLogFieldError(f"fields outside the closed set: {', '.join(unknown)}")
    invalid = sorted(name for name, value in fields.items() if not _valid(name, value))
    if invalid:
        raise InvalidLogValueError(f"values without the shape of their field: {', '.join(invalid)}")
    return json.dumps(fields, sort_keys=True, ensure_ascii=True, allow_nan=False)


class ServiceLog:
    """Writes events to every sink, one line each. It is the only writer the service has."""

    def __init__(self, sinks: Sequence[TextIO]) -> None:
        self._sinks = tuple(sinks)
        self._lock = threading.Lock()

    def event(self, **fields: object) -> None:
        """Write one line with ``fields``; see ``format_event`` for what is refused."""
        line = format_event(**fields) + "\n"
        with self._lock:
            for sink in self._sinks:
                sink.write(line)
                sink.flush()
