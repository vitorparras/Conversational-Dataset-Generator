"""Recognizers behind the common protocol."""

from language_service.recognizers.base import Recognizer
from language_service.recognizers.empty import EmptyRecognizer

__all__ = ["EmptyRecognizer", "Recognizer"]
