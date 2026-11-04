"""The parts a critical core area may be mutated in: `Core.Persistence.Content@2/3` is the second of
three jobs of one area, each mutating only its share of the files with the same test project, the
same ceiling and the same survivor limit. The files are sorted by path and dealt out in turn, so
the split depends on nothing but the files, and every file belongs to exactly one part."""

from __future__ import annotations

import re
from dataclasses import dataclass

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


def share(files: list[str], part: Part) -> tuple[str, ...]:
    """The files of one part: the sorted files dealt out in turn among the parts."""
    return tuple(sorted(files)[part.number - 1 :: part.of])


def parts_of(count: int) -> tuple[Part | None, ...]:
    """The parts an area of ``count`` parts runs in; none to name when it is a single part."""
    return (None,) if count == 1 else tuple(Part(n, count) for n in range(1, count + 1))
