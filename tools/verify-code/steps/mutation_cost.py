"""What testing the mutants of a member of a C# file costs, and where a long static list may be cut
(#683): Stryker cannot say which tests reach a static initializer, so it runs the whole suite for
each of its mutants, and the slices of a file are weighed by that cost, not only by their tokens."""

from __future__ import annotations

from collections.abc import Iterator
from itertools import pairwise

from steps.csharp_tokens import Token, strings, tokens

#: What one mutant that runs the whole suite weighs, in tokens of other code. Stryker cannot say
#: which tests reach a static initializer, so each of its mutants runs the whole suite (#683), and
#: a part takes as long as the mutants of the suite it holds: in run 44636875465 (#849) one took
#: 2 to 2,5 min, while a part of 13 mutants of other code, some 35 tokens each, took no more than
#: the 4,5 to 5 min of the preparation. Each literal of a static initializer, a string or a
#: boolean, is such a mutant.
SUITE_MUTANT = 1000
BOOLEANS = frozenset({"true", "false"})
#: The methods of a file, by its path in the project and their signature (name and parameters),
#: that every test of the area runs through, so that Stryker tests each of their mutants against
#: the whole suite; nothing in the text says so, hence the signatures. Each of their tokens weighs
#: a mutant of the suite, so each is alone in its part (#849: alone, the ``OpenAsync`` that opened
#: every content transaction took 15,5 min of the 20, and was cut in two).
SUITE_WIDE: dict[str, frozenset[str]] = {
    "Content/ContentTransaction.cs": frozenset(
        {
            "OpenAsync(NpgsqlDataSource dataSource, Declaration declare, IsolationLevel isolation,"
            " CancellationToken cancellationToken)",
            "DeclareAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,"
            " Declaration declare, CancellationToken cancellationToken)",
        }
    ),
}
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


def signature(member: list[Token]) -> tuple[str, ...]:
    """The tokens of the signature of the method or constructor ``member`` declares: from the word
    before its first parenthesis outside every group to the one that closes it, when no ``=``
    nor ``=>`` comes before it; none for anything else, so a call in an initializer or an
    expression body declares nothing."""
    opened = parameters(member)
    closed = None if opened is None else closing(member, opened, len(member))
    if not opened or closed is None:
        return ()
    return tuple(token.kind for token in member[opened - 1 : closed + 1])


def parameters(member: list[Token]) -> int | None:
    """The position of the first parenthesis of ``member`` outside every group, or None when an
    ``=``, an ``=>`` or a ``;`` comes first."""
    for position in top_level(member, 0, len(member)):
        kind = member[position].kind
        if kind in {"=", "=>", ";"}:
            return None
        if kind == "(":
            return position
    return None


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
    a static initializer holding a list; the weight is the tokens, plus ``SUITE_MUTANT`` for each
    literal of a static initializer, wherever it is cut."""
    member = found[first:last]
    static = static_initializer(member)
    inner = element_cuts(text, found, first, last) if static else []
    marks = [first, *inner, last]
    return [
        (b, max(1, b - a) + (literals(text, found, a, b) * SUITE_MUTANT if static else 0))
        for a, b in pairwise(marks)
    ]


def literals(text: str, found: list[Token], first: int, last: int) -> int:
    """The literals Stryker mutates in ``found[first:last]``: its strings and its booleans."""
    start = found[first - 1].end if first > 0 else 0
    words = sum(token.kind in BOOLEANS for token in found[first:last])
    return words + strings(text, start, found[last - 1].end if last > first else start)


def suite_factor(member: list[Token], suite: frozenset[str]) -> int:
    """``SUITE_MUTANT`` for a method whose signature is in ``suite``, which every test runs
    through; 1 for any other member."""
    declared = signature(member)
    return SUITE_MUTANT if any(declared == signed(entry) for entry in suite) else 1


def signed(entry: str) -> tuple[str, ...]:
    """The tokens of a signature written in ``SUITE_WIDE``."""
    return tuple(token.kind for token in tokens(entry))
