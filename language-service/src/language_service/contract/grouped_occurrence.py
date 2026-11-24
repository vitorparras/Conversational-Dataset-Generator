"""One occurrence of an item, placed back in the text it was found in."""

from language_service.contract.message import Message


class GroupedOccurrence(Message):
    """Its text by index, a half-open span of code points in that text, its analysis and group."""

    text_index: int
    start: int
    end: int
    kind: str
    part_of_speech: str
    syntactic_role: str
    group: int
