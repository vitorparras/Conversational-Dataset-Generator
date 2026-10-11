"""The recognizer of the foundation phase: it analyses and finds nothing."""

from collections.abc import Sequence

from language_service.contract import Occurrence


class EmptyRecognizer:
    """Always answers "analysed, nothing found". It has no model, so it reports none."""

    @property
    def model_version(self) -> str | None:
        """No model is loaded."""
        return None

    def recognize(self, text: str, language: str) -> Sequence[Occurrence]:
        """Return no occurrence, whatever the text."""
        return ()
