"""A recognizer double that answers whatever the test gives it, so a route is seen passing it on."""

import re
import time
from collections.abc import Sequence
from dataclasses import dataclass, field

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


@dataclass(frozen=True)
class RaisingRecognizer:
    """Fails on every text with a message that quotes it, as a careless library would."""

    model_version: str | None = None

    def recognize(self, text: str, language: str) -> Sequence[Occurrence]:
        raise ValueError(f"could not analyse {text!r}")


@dataclass(frozen=True)
class RaisingVersionRecognizer:
    """Finds nothing, then fails when asked for its model version, which the response needs."""

    @property
    def model_version(self) -> str | None:
        raise ValueError(f"no version for {MARKER}")

    def recognize(self, text: str, language: str) -> Sequence[Occurrence]:
        return ()


@dataclass(frozen=True)
class SlowRecognizer:
    """Finds nothing after waiting a known time, so the logged elapsed time has a floor."""

    delay_seconds: float
    model_version: str | None = None

    def recognize(self, text: str, language: str) -> Sequence[Occurrence]:
        time.sleep(self.delay_seconds)
        return ()


@dataclass(frozen=True)
class Word:
    """A word the double finds wherever it is, with the kind and part of speech it reports."""

    text: str
    kind: str = "PERSON"
    part_of_speech: str = "PROPN"


@dataclass(frozen=True)
class WordRecognizer:
    """Finds every place of each word it was given in a text, and keeps the texts it was asked."""

    words: tuple[Word, ...]
    model_version: str | None = None
    asked: list[str] = field(default_factory=list[str])

    def recognize(self, text: str, language: str) -> Sequence[Occurrence]:
        """The longest word wins where two overlap, as a recognizer gives the whole name."""
        self.asked.append(text)
        by_text = {word.text: word for word in self.words}
        longest_first = sorted(by_text, key=len, reverse=True)
        pattern = "|".join(rf"(?<!\w){re.escape(word)}(?!\w)" for word in longest_first)
        return [
            Occurrence(
                match.start(),
                match.end(),
                by_text[match.group()].kind,
                by_text[match.group()].part_of_speech,
                "nsubj",
            )
            for match in re.finditer(pattern, text)
        ]
