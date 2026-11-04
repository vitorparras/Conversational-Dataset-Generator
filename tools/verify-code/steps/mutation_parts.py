"""The parts a critical core area may be mutated in: `Core.Persistence.Content@2/3` is the second of
three jobs of one area, each mutating only its share of the code with the same test project, the
same ceiling and the same survivor limit. The unit of the split is the slice of a file, cut between
the members of its types (steps.mutation_members); the slices are sorted by path and position and
dealt out in turn, so the split depends on nothing but the files, and every slice belongs to
exactly one part (#585: a file cannot be split, but the members of one big file can)."""

from __future__ import annotations

import re
from dataclasses import dataclass

from steps.mutation_members import UNBOUNDED

SEPARATOR = "@"
SHAPE = re.compile(r"([1-9][0-9]*)/([1-9][0-9]*)")


@dataclass(frozen=True)
class Part:
    """The ``number``-th of ``of`` parts of an area, counted from 1."""

    number: int
    of: int

    @property
    def label(self) -> str:
        return f"{self.number}/{self.of}"

    def holds(self, count: int) -> bool:
        """Whether this is one of the ``count`` parts an area of that many parts is run in."""
        return self.of == count and self.number <= count


def module_name(prefix: str, part: Part | None) -> str:
    """The name CI hands to ``--module``: the prefix, with the part when the area has several."""
    return prefix if part is None else f"{prefix}{SEPARATOR}{part.label}"


def split_module(module: str | None) -> tuple[str | None, Part | None, str | None]:
    """The prefix and the part a ``--module`` names, and the problem when the part is malformed."""
    if module is None or SEPARATOR not in module:
        return module, None, None
    prefix, _, shape = module.partition(SEPARATOR)
    found = SHAPE.fullmatch(shape)
    if found is None:
        shape_of = f"`{module}`: a parte tem a forma `{SEPARATOR}N/M`, com inteiros positivos"
        return prefix, None, shape_of
    return prefix, Part(int(found[1]), int(found[2])), None


@dataclass(frozen=True, order=True)
class Slice:
    """The characters ``start`` to ``end`` of a file, as Stryker counts them."""

    file: str
    start: int
    end: int

    @property
    def whole(self) -> bool:
        return self.start == 0 and self.end == UNBOUNDED


def dealt(slices: list[Slice], part: Part) -> list[Slice]:
    """The slices of one part: the sorted slices dealt out in turn among the parts."""
    return sorted(slices)[part.number - 1 :: part.of]


def share(slices: list[Slice], part: Part) -> tuple[str, ...]:
    """The mutate patterns of one part: the slices of a file in a pattern of its own with one span
    each, and a whole file as its bare path."""
    mine = dealt(slices, part)
    files = dict.fromkeys(piece.file for piece in mine)
    return tuple(pattern(file, [piece for piece in mine if piece.file == file]) for file in files)


def pattern(file: str, pieces: list[Slice]) -> str:
    """The pattern for ``pieces`` of one ``file``: ``File.cs{0..120}{300..480}``."""
    if len(pieces) == 1 and pieces[0].whole:
        return file
    return file + "".join(f"{{{piece.start}..{piece.end}}}" for piece in pieces)


def parts_of(count: int) -> tuple[Part | None, ...]:
    """The parts an area of ``count`` parts runs in; none to name when it is a single part."""
    return (None,) if count == 1 else tuple(Part(n, count) for n in range(1, count + 1))
