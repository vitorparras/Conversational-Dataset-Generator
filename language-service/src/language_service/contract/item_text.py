"""One text of an item, as the core sends it."""

from language_service.contract.message import Message


class ItemText(Message):
    """The text of one message and the name of its author; None when the text has no author."""

    text: str
    author: str | None
