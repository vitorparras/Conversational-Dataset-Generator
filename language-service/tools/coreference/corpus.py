"""The gold corpus: synthetic conversations with every mention of a person marked `[text|id]`."""

from __future__ import annotations

import json
import re
from collections.abc import Sequence
from dataclasses import dataclass
from pathlib import Path

MARK = re.compile(r"\[([^\[\]|]+)\|(\w+)\]")


@dataclass(frozen=True)
class Span:
    """Code point offsets in the joined text, end exclusive."""

    start: int
    end: int


@dataclass(frozen=True)
class Message:
    """One message: who wrote it, the name it carries and the marked text."""

    author: str
    name: str
    text: str
    mixed: bool = False


@dataclass(frozen=True)
class Mention:
    """A marked person; `author_name` is the name before a message, never an occurrence."""

    span: Span
    entity: str
    mixed: bool
    author_name: bool


@dataclass(frozen=True)
class Conversation:
    """The text the service receives, one `name: text` line per message, and its gold mentions."""

    text: str
    mentions: tuple[Mention, ...]
    authors: frozenset[str]


def load(path: Path) -> tuple[tuple[Message, ...], ...]:
    """The conversations of a corpus file, each as its messages in order."""
    raw = json.loads(path.read_text(encoding="utf-8"))
    return tuple(
        tuple(
            Message(m["author"], m["name"], m["text"], bool(m.get("mixed", False)))
            for m in conversation["messages"]
        )
        for conversation in raw["conversations"]
    )


def _line(message: Message, start: int) -> tuple[str, list[Mention]]:
    name = Span(start, start + len(message.name))
    mentions = [Mention(name, message.author, message.mixed, author_name=True)]
    line, cursor = f"{message.name}: ", 0
    for mark in MARK.finditer(message.text):
        line += message.text[cursor : mark.start()]
        at = start + len(line)
        mentions.append(Mention(Span(at, at + len(mark[1])), mark[2], message.mixed, False))
        line, cursor = line + mark[1], mark.end()
    line += message.text[cursor:]
    if "[" in line or "]" in line:
        raise ValueError(f"malformed mark in a corpus message: {message.text!r}")
    return line + "\n", mentions


def join(messages: Sequence[Message]) -> Conversation:
    """Joins the messages the way the service request does and places the gold mentions."""
    text, mentions = "", list[Mention]()
    for message in messages:
        line, found = _line(message, len(text))
        text, mentions = text + line, mentions + found
    return Conversation(text, tuple(mentions), frozenset(m.author for m in messages))
