"""The response of the item analysis route."""

from language_service.contract.author_group import AuthorGroup
from language_service.contract.grouped_occurrence import GroupedOccurrence
from language_service.contract.message import Message


class AnalyzeItemResponse(Message):
    """Every occurrence in its text with its group, and one group for each author of the item."""

    occurrences: tuple[GroupedOccurrence, ...]
    author_groups: tuple[AuthorGroup, ...]
    model_version: str | None
