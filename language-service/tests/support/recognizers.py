"""A recognizer double that answers whatever the test gives it, so a route is seen passing it on."""

from collections.abc import Sequence
from dataclasses import dataclass

from language_service.recognizers.occurrence import Occurrence

OCCURRENCE = Occurrence(start=0, end=3, kind="PERSON", part_of_speech="PROPN", syntactic_role="obj")
OCCURRENCE_JSON = {
    "start": 0,
    "end": 3,
    "kind": "PERSON",
    "part_of_speech": "PROPN",
    "syntactic_role": "obj",
}


@dataclass(frozen=True)
class FixedRecognizer:
    """Finds the same occurrences in every text and reports the model version it was given."""

    found: tuple[Occurrence, ...]
    model_version: str | None

    def recognize(self, text: str, language: str) -> Sequence[Occurrence]:
        return self.found
