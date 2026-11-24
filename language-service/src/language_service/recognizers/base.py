"""The port every recognizer fills."""

from collections.abc import Sequence
from typing import Protocol

from language_service.recognizers.mention import Mention
from language_service.recognizers.occurrence import Occurrence


class Recognizer(Protocol):
    """Finds occurrences in a text, or mentions and their entities in an item, holding no state."""

    @property
    def model_version(self) -> str | None:
        """The version of the model behind the answers; None when there is no model."""
        ...

    def recognize(self, text: str, language: str) -> Sequence[Occurrence]:
        """The occurrences found, in code point offsets; empty when there is none."""
        ...

    def resolve(self, text: str, language: str) -> Sequence[Mention]:
        """The mentions in the joined text of an item, each with the group of its entity."""
        ...
