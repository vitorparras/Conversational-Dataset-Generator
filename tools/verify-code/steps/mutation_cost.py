"""What testing the mutants of a member of a C# file costs, and where a long static list may be cut
(#683): Stryker cannot say which tests reach a static initializer, so it runs the whole suite for
each of its mutants, and the slices of a file are weighed by that cost, not only by their tokens."""

from __future__ import annotations

from collections.abc import Iterator
from itertools import pairwise

from steps.csharp_tokens import Token

#: What one token of a static initializer costs against one of any other code. Stryker cannot say
#: which tests reach a static initializer, so each of its mutants runs the whole suite (#683: the
#: 24 mutants of ``ContentColumns`` took 22 min, 304 tests each, while the same weight in tokens
#: anywhere else took 4 to 11 min). Measured between 4 and 5.
STATIC_COST = 5
OPENERS = frozenset("{([")
CLOSERS = frozenset("})]")
ANGLES = frozenset("<>")
STATIC = "static"


def top_level(found: list[Token], first: int, last: int) -> Iterator[int]:
    """The positions in ``found[first:last]`` of the tokens outside every bracket, brace and
    parenthesis: the one that opens a group is among them; what is inside it, and what closes
    it, is not."""
    depth = 0
    for position in range(first, last):
        kind = found[position].kind
        if kind in CLOSERS:
            depth = max(0, depth - 1)
            continue
        if depth == 0:
            yield position
        if kind in OPENERS:
            depth += 1


def static_initializer(member: list[Token]) -> bool:
    """Whether the member is a static field or property with an initializer, or a static
    constructor: what runs once, before any test, so Stryker tests each of its mutants against
    every test. What is inside brackets, braces or parentheses says nothing about the member."""
    after_static = -1
    for position in top_level(member, 0, len(member)):
        kind = member[position].kind
        if kind == STATIC:
            after_static = position + 1
        elif kind == ";":
            after_static = -1
        elif after_static >= 0 and kind == "(":
            return position - after_static == 1
        elif after_static >= 0 and kind == "=":
            return True
    return False


def closing(found: list[Token], opened: int, last: int) -> int | None:
    """The position of the token that closes the group ``found[opened]`` opens, before ``last``."""
    depth = 0
    for position in range(opened, last):
        kind = found[position].kind
        depth += (kind in OPENERS) - (kind in CLOSERS)
        if depth == 0:
            return position
    return None


def list_bounds(found: list[Token], first: int, last: int) -> tuple[int, int] | None:
    """The positions of the ``[`` and the ``]`` of the collection expression that initializes the
    member of ``found[first:last]`` (``= [ ... ]``), or None when it has none."""
    for position in top_level(found, first, last):
        if position > first and found[position].kind == "[" and found[position - 1].kind == "=":
            closed = closing(found, position, last)
            return None if closed is None else (position, closed)
    return None


def commas(found: list[Token], opened: int, closed: int) -> list[int]:
    """The positions, exclusive, after each comma of the list ``found[opened]``..``found[closed]``
    that separates two of its elements: not the ones inside an element, nor a trailing one."""
    cuts: list[int] = []
    depth = 0
    for position in range(opened, closed):
        kind = found[position].kind
        depth += (kind in OPENERS) - (kind in CLOSERS)
        if kind == "," and depth == 1 and found[position + 1].kind != "]":
            cuts.append(position + 1)
    return cuts


def element_cuts(text: str, found: list[Token], first: int, last: int) -> list[int]:
    """The token positions, exclusive, at which the member of ``found[first:last]`` may also be cut:
    after each comma between the elements of the collection expression that initializes it
    (``= [ new(...), new(...) ]``), so a long list is not one unit (#683). None when the list holds
    a ``<`` or a ``>`` (generic arguments, lambdas), where this reading of the tokens could take a
    comma of an element for one of the list. Stryker 5.0.0 makes no mutant of the list itself, only
    of its elements, and each element lies whole in one slice."""
    bounds = list_bounds(found, first, last)
    if bounds is None:
        return []
    opened, closed = bounds
    inside = text[found[opened].end : found[closed].end]
    return [] if set(inside) & ANGLES else commas(found, opened, closed)


def pieces(text: str, found: list[Token], first: int, last: int) -> list[tuple[int, int]]:
    """The cuts of one member as ``(end, weight)``: the member itself, or its elements when it is
    a static initializer holding a list; the weight is the tokens, times ``STATIC_COST`` for a
    static initializer, wherever it is cut."""
    member = found[first:last]
    static = static_initializer(member)
    inner = element_cuts(text, found, first, last) if static else []
    marks = [first, *inner, last]
    factor = STATIC_COST if static else 1
    return [(b, max(1, b - a) * factor) for a, b in pairwise(marks)]
