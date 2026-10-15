"""The recognizer of the foundation phase: it analyses and finds nothing."""

from collections.abc import Sequence

from language_service.recognizers.base import Occurrence


class EmptyRecognizer:
    """Always answers "analysed, nothing found"; it has no model, so it reports none."""

    @property
    def model_version(self) -> str | None:
        return None

    def recognize(self, text: str, language: str) -> Sequence[Occurrence]:
        return ()
