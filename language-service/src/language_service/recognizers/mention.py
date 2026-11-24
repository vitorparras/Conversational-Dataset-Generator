"""What a recognizer speaks of in a whole item: an occurrence in the joined text and its entity."""

from dataclasses import dataclass

from language_service.recognizers.occurrence import Occurrence


@dataclass(frozen=True)
class Mention:
    """An occurrence over the joined text of an item; the mentions of one entity share a group."""

    occurrence: Occurrence
    group: int
