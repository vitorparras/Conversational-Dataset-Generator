"""The response of the analysis route."""

from language_service.contract.message import Message
from language_service.recognizers.occurrence import Occurrence


class AnalyzeResponse(Message):
    """An empty list means analysed and nothing found; an unavailable service never answers."""

    occurrences: tuple[Occurrence, ...]
    model_version: str | None
