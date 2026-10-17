"""The port every recognizer fills, and the occurrence it speaks of."""

from collections.abc import Sequence
from dataclasses import dataclass
from typing import Protocol


@dataclass(frozen=True)
class Occurrence:
    """A half-open span of code points, its kind, and the part of speech and role of its head."""

    start: int
    end: int
    kind: str
    part_of_speech: str
    syntactic_role: str


class Recognizer(Protocol):
    """Finds occurrences in a text and holds no state about earlier texts."""

    @property
    def model_version(self) -> str | None:
        """The version of the model behind the answers; None when there is no model."""
        ...

    def recognize(self, text: str, language: str) -> Sequence[Occurrence]:
        """The occurrences found, in code point offsets; empty when there is none."""
        ...
