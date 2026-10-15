"""A scan of source text for the switches the service must not have: a debug or reload mode."""

import re
from typing import Final

FORBIDDEN_SWITCHES: Final = ("debug", "reload")
PATTERN: Final = re.compile(r"\b(" + "|".join(FORBIDDEN_SWITCHES) + r")\b", re.IGNORECASE)


def forbidden_switches(text: str) -> tuple[str, ...]:
    """Every forbidden word found in ``text``, lower-cased, in order of appearance."""
    return tuple(match.lower() for match in PATTERN.findall(text))
