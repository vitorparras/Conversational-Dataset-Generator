"""The port every recognizer fills."""

from collections.abc import Sequence
from typing import Protocol

from language_service.recognizers.occurrence import Occurrence


class Recognizer(Protocol):
    """Finds occurrences in a text and holds no state about earlier texts."""

    @property
    def model_version(self) -> str | None:
        """The version of the model behind the answers; None when there is no model."""
        ...

    def recognize(self, text: str, language: str) -> Sequence[Occurrence]:
        """The occurrences found, in code point offsets; empty when there is none."""
        ...
