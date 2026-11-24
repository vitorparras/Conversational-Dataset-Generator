"""The group of the entity an author of an item is."""

from language_service.contract.message import Message


class AuthorGroup(Message):
    """An author named in the request and the group its mentions share; never an occurrence."""

    author: str
    group: int
