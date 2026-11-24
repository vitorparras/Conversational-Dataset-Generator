"""A recognizer double that answers whatever the test gives it, so a route is seen passing it on."""

import re
import time
from collections.abc import Sequence
from dataclasses import dataclass

from language_service.recognizers.mention import Mention
from language_service.recognizers.occurrence import Occurrence
from support.synthetic import MARKER

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

    def resolve(self, text: str, language: str) -> Sequence[Mention]:
        return ()


@dataclass(frozen=True)
class RaisingRecognizer:
    """Fails on every text with a message that quotes it, as a careless library would."""

    model_version: str | None = None

    def recognize(self, text: str, language: str) -> Sequence[Occurrence]:
        raise ValueError(f"could not analyse {text!r}")

    def resolve(self, text: str, language: str) -> Sequence[Mention]:
        return ()


@dataclass(frozen=True)
class RaisingVersionRecognizer:
    """Finds nothing, then fails when asked for its model version, which the response needs."""

    @property
    def model_version(self) -> str | None:
        raise ValueError(f"no version for {MARKER}")

    def recognize(self, text: str, language: str) -> Sequence[Occurrence]:
        return ()

    def resolve(self, text: str, language: str) -> Sequence[Mention]:
        return ()


@dataclass(frozen=True)
class SlowRecognizer:
    """Finds nothing after waiting a known time, so the logged elapsed time has a floor."""

    delay_seconds: float
    model_version: str | None = None

    def recognize(self, text: str, language: str) -> Sequence[Occurrence]:
        time.sleep(self.delay_seconds)
        return ()

    def resolve(self, text: str, language: str) -> Sequence[Mention]:
        return ()


@dataclass(frozen=True)
class ResolvingRecognizer:
    """Finds every place of each word it was given in the joined text of an item, with its group."""

    groups: tuple[tuple[str, int], ...]
    model_version: str | None = None

    def recognize(self, text: str, language: str) -> Sequence[Occurrence]:
        return ()

    def resolve(self, text: str, language: str) -> Sequence[Mention]:
        found = (
            Mention(Occurrence(match.start(), match.end(), "PERSON", "PROPN", "nsubj"), group)
            for word, group in self.groups
            for match in re.finditer(re.escape(word), text)
        )
        return sorted(found, key=lambda mention: mention.occurrence.start)
