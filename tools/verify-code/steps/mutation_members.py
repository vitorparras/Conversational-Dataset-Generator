"""Where the members of a C# type end, so a file can be cut into slices between members (#585).

Stryker keeps a mutant only when the node it mutates lies whole inside the span of a slice
(``File.cs{a..b}``): a cut never inside a member leaves each node in exactly one slice. The one
cut inside a member is between the elements of the list that initializes a static member, and a
slice weighs what its mutants cost to test, not only its tokens (#683)."""

from __future__ import annotations

from bisect import bisect_right
from dataclasses import dataclass, field
from itertools import pairwise

from steps.csharp_tokens import ARROW, Token, tokens
from steps.mutation_cost import pieces

#: The largest index of a file: the last slice runs to it, as a pattern with no span does.
UNBOUNDED = 2**31 - 1
TYPE_WORDS = frozenset({"class", "struct", "interface", "enum", "record"})
ASSIGNMENTS = frozenset({"=", ARROW})
#: What may follow the brace that closes a body without ending the member (``{ get; } = 1;``).
CONTINUES = frozenset({"=", ";", ",", ")", ARROW})
NAMESPACE = "ns"
TYPE = "type"
BODY = "body"


@dataclass
class Header:
    """What the words since the last brace or semicolon say about the block that comes next."""

    namespace: bool = False
    typed: bool = False
    paren_seen: bool = False
    assigned: bool = False
    parens: int = 0
    brackets: int = 0

    @property
    def plain(self) -> bool:
        """Whether nothing is open: no parenthesis, no bracket."""
        return self.parens == 0 and self.brackets == 0

    def feed(self, kind: str) -> None:
        """Takes one token of the header; what is inside brackets or parentheses says nothing."""
        if kind in "[]" and len(kind) == 1:
            self.brackets = max(0, self.brackets + (1 if kind == "[" else -1))
        elif self.brackets:
            return
        elif kind in "()" and len(kind) == 1:
            self.parens = max(0, self.parens + (1 if kind == "(" else -1))
            self.paren_seen = True
        elif self.parens:
            return
        elif kind in ASSIGNMENTS:
            self.assigned = True
        elif kind == "namespace":
            self.namespace = True
        elif kind in TYPE_WORDS and not (self.paren_seen or self.assigned):
            self.typed = True

    def block(self) -> str:
        """What the brace that follows opens: a namespace, a type or the body of a member."""
        if self.namespace:
            return NAMESPACE
        return TYPE if self.typed and not self.assigned else BODY


@dataclass
class Frame:
    """An open brace: its kind, whether its close ends a member, and the header it interrupted."""

    kind: str
    ends_member: bool
    saved: Header


@dataclass
class Walk:
    """The walk over the tokens: the open braces and where members end."""

    stack: list[Frame] = field(default_factory=list[Frame])
    header: Header = field(default_factory=Header)
    ends: list[int] = field(default_factory=list[int])
    failed: bool = False

    @property
    def member_level(self) -> bool:
        """Whether the walk is directly inside a type that no other type holds."""
        inside = bool(self.stack) and self.stack[-1].kind == TYPE
        return inside and all(frame.kind == NAMESPACE for frame in self.stack[:-1])

    def step(self, token: Token, following: str) -> None:
        if token.kind == "{":
            self.opens()
        elif token.kind == "}":
            self.closes(token.end, following)
        elif token.kind == ";":
            self.semicolon(token.end)
        else:
            self.header.feed(token.kind)

    def opens(self) -> None:
        header = self.header
        kind = header.block()
        ends = kind == BODY and self.member_level and header.plain and not header.assigned
        self.stack.append(Frame(kind, ends, header))
        self.header = Header()

    def closes(self, end: int, following: str) -> None:
        if not self.stack:
            self.failed = True
            return
        frame = self.stack.pop()
        if frame.kind == BODY and not frame.ends_member:
            self.header = frame.saved
            return
        self.header = Header()
        nested = frame.kind == TYPE and self.member_level
        if (frame.ends_member or nested) and following not in CONTINUES:
            self.ends.append(end)

    def semicolon(self, end: int) -> None:
        if self.member_level and self.header.plain:
            self.ends.append(end)
        self.header = Header()


def member_ends(text: str) -> tuple[int, ...]:
    """The indexes just after each member of the types of ``text``; none when the braces of the
    file do not close, so a file that is not understood is never cut."""
    return ends_of(tokens(text))


def ends_of(found: list[Token]) -> tuple[int, ...]:
    """``member_ends`` over tokens already read."""
    walk = Walk()
    for position, token in enumerate(found):
        following = found[position + 1].kind if position + 1 < len(found) else ""
        walk.step(token, following)
    return () if walk.stack or walk.failed else tuple(walk.ends)


def units(text: str, index: int) -> int:
    """The UTF-16 units before ``index``: the offsets Stryker counts the spans in."""
    return len(text[:index].encode("utf-16-le")) // 2


def slices(text: str) -> tuple[tuple[int, int, int], ...]:
    """The slices of a file as ``(start, end, weight)``: offsets from the first character to
    ``UNBOUNDED``, cut after each member and, in a static list, after each element; the weight is
    the tokens, so comments weigh nothing, times ``STATIC_COST`` for a static initializer."""
    found = tokens(text)
    ends = [token.end for token in found]
    members = [0, *(bisect_right(ends, cut) for cut in ends_of(found)), len(ends)]
    cut = [item for a, b in pairwise(members) for item in pieces(text, found, a, b)]
    bounds = (0, *(units(text, found[end - 1].end) for end, _ in cut[:-1]), UNBOUNDED)
    return tuple((a, b, w) for (a, b), (_, w) in zip(pairwise(bounds), cut, strict=True))
