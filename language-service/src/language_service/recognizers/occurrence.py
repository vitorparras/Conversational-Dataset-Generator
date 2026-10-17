"""What a recognizer speaks of: one occurrence in a text, by code point offsets."""

from dataclasses import dataclass


@dataclass(frozen=True)
class Occurrence:
    """A half-open span of code points, its kind, and the part of speech and role of its head."""

    start: int
    end: int
    kind: str
    part_of_speech: str
    syntactic_role: str
