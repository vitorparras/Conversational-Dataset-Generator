"""What every recognizer offers the application."""

from collections.abc import Sequence
from typing import Protocol

from language_service.contract import Occurrence


class Recognizer(Protocol):
    """Finds occurrences in a text. Implementations hold no state about earlier texts."""

    @property
    def model_version(self) -> str | None:
        """The version of the model behind the answers, or None when there is no model."""
        ...

    def recognize(self, text: str, language: str) -> Sequence[Occurrence]:
        """Return the occurrences found, in code point offsets; an empty sequence if there are none."""
        ...
