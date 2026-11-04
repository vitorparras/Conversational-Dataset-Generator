"""The tokens of a C# file that tell where a member ends, without comments or literals."""

from __future__ import annotations

import re
from typing import NamedTuple

PUNCTUATION = frozenset("{};()[]=,")
WORD = re.compile(r"@?[^\W\d]\w*")
PREFIX = re.compile(r'[$@]+(?=")')
QUOTES = re.compile(r'"+')
BRACES = re.compile(r"\{+")
ARROW = "=>"
RAW_QUOTES = 3


class Token(NamedTuple):
    """A word, a mark of ``PUNCTUATION`` or the arrow, and the index just after it."""

    kind: str
    end: int


def tokens(text: str) -> list[Token]:
    """The tokens of ``text`` in order; none from a comment, a literal or a directive."""
    found: list[Token] = []
    i = 0
    while i < len(text):
        skipped = skipped_end(text, i)
        i = take(text, i, found) if skipped is None else skipped
    return found


def take(text: str, i: int, found: list[Token]) -> int:
    """Reads the token at ``i`` into ``found``, if there is one, and says where the next starts."""
    word = WORD.match(text, i)
    if word is not None:
        found.append(Token(word.group(), word.end()))
        return word.end()
    if text.startswith(ARROW, i):
        found.append(Token(ARROW, i + len(ARROW)))
        return i + len(ARROW)
    if text[i] in PUNCTUATION:
        found.append(Token(text[i], i + 1))
    return i + 1


def skipped_end(text: str, i: int) -> int | None:
    """Where the directive, comment or literal that starts at ``i`` ends; None if none does."""
    if text[i] == "#" and not text[text.rfind("\n", 0, i) + 1 : i].strip():
        return line_end(text, i)
    return literal_end(text, i)


def line_end(text: str, i: int) -> int:
    found = text.find("\n", i)
    return len(text) if found < 0 else found


def literal_end(text: str, i: int) -> int | None:
    """Where the comment, string or character literal that starts at ``i`` ends; None if none."""
    if text.startswith("//", i):
        return line_end(text, i)
    if text.startswith("/*", i):
        found = text.find("*/", i + 2)
        return len(text) if found < 0 else found + 2
    if text[i] == "'":
        return char_end(text, i)
    prefix = PREFIX.match(text, i)
    start = i if prefix is None else prefix.end()
    if text.startswith('"', start):
        return string_end(text, start, "" if prefix is None else prefix.group())
    return None


def char_end(text: str, i: int) -> int:
    j = i + 1
    while j < len(text) and text[j] != "'":
        j += 2 if text[j] == "\\" else 1
    return min(j + 1, len(text))


def string_end(text: str, start: int, prefix: str) -> int:
    """Where the string whose first quote is at ``start`` ends; ``prefix`` is its ``$``/``@``."""
    verbatim = "@" in prefix
    dollars = prefix.count("$")
    quotes = QUOTES.match(text, start)
    count = 0 if quotes is None else quotes.end() - start
    if count >= RAW_QUOTES and not verbatim:
        return raw_end(text, start + count, count, dollars)
    j = start + 1
    while j < len(text):
        step = string_step(text, j, verbatim, dollars)
        if step < 0:
            return -step
        j = step
    return len(text)


def string_step(text: str, j: int, verbatim: bool, dollars: int) -> int:
    """The index after the character at ``j`` of a string; minus the index after the string, when
    that character closes it."""
    char = text[j]
    if char == "\\" and not verbatim:
        return j + 2
    if char == '"':
        return j + 2 if verbatim and text.startswith('""', j) else -(j + 1)
    if dollars and char in "{}":
        return hole_step(text, j, char)
    return j + 1


def hole_step(text: str, j: int, char: str) -> int:
    """After a brace of an interpolated string: a doubled one is text, a lone ``{`` opens code."""
    if text.startswith(char * 2, j):
        return j + 2
    return hole_end(text, j + 1) if char == "{" else j + 1


def hole_end(text: str, j: int) -> int:
    """The index after the ``}`` that closes the hole of code that starts at ``j``."""
    depth = 1
    while j < len(text) and depth:
        skipped = literal_end(text, j)
        if skipped is not None:
            j = skipped
            continue
        depth += (text[j] == "{") - (text[j] == "}")
        j += 1
    return j


def raw_end(text: str, j: int, quotes: int, dollars: int) -> int:
    """Where the raw string whose opening ``quotes`` end at ``j`` ends; its holes of code open with
    as many braces as it has ``$``."""
    closing = '"' * quotes
    while j < len(text) and not text.startswith(closing, j):
        braces = BRACES.match(text, j)
        run = 0 if braces is None else braces.end() - j
        j = hole_end(text, j + run) if dollars and run >= dollars else j + max(run, 1)
    return min(j + quotes, len(text))
