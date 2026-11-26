"""The parts a critical core area may be mutated in: `Core.Persistence.Content@2/3` is the second of
three jobs of one area, each mutating only its share of the code with the same test project, the
same ceiling and the same survivor limit. The unit of the split is the slice of a file, cut between
the members of its types (steps.mutation_members), weighted by its tokens; the heaviest slice goes
first to the part it adds least to, so the split depends on nothing but the files and every slice
belongs to exactly one part (#585: dealt in turn by count, one part got 40 mutants)."""

from __future__ import annotations

import heapq
import re
from dataclasses import dataclass
from functools import lru_cache
from math import ceil

from steps.mutation_cost import COVERAGE, SUITE_MUTANT, Cost
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
    weight: int = 1


def dealt(slices: list[Slice], part: Part) -> list[Slice]:
    """The slices of one part, as ``layout`` places them."""
    ordered = tuple(sorted(slices, key=lambda s: (-s.weight, s.file, s.start)))
    places = layout(ordered, part.of)
    return sorted(p for p, n in zip(ordered, places, strict=True) if n == part.number - 1)


@lru_cache(maxsize=8)
def layout(ordered: tuple[Slice, ...], parts: int) -> tuple[int, ...]:
    """The part of each slice, the heaviest first: the slices that need the coverage of each test
    in parts of their own, as many as make the heaviest part cost least, when that spares it more
    than a mutant of the suite (#935); otherwise each slice to the part with the least weight."""
    weights = [int(piece.weight) for piece in ordered]
    needs = [covered(piece) for piece in ordered]
    together = spread(weights, parts)
    if parts == 1 or all(needs) or not any(needs):
        return tuple(together)
    split_up = min(
        (apart(weights, needs, parts, plain) for plain in range(1, parts)),
        key=lambda places: cost(weights, places, needs, parts),
    )
    saved = cost(weights, together, needs, parts) - cost(weights, split_up, needs, parts)
    return tuple(split_up if saved > SUITE_MUTANT else together)


def spread(weights: list[int], parts: int) -> list[int]:
    """The part of each weight, the heaviest first: the one with the least weight so far, the
    first of them on a tie."""
    heap = [(0, number) for number in range(parts)]
    places: list[int] = []
    for weight in weights:
        load, number = heapq.heappop(heap)
        places.append(number)
        heapq.heappush(heap, (load + weight, number))
    return places


def apart(weights: list[int], needs: list[bool], parts: int, plain: int) -> list[int]:
    """The weights that need the coverage spread in the first ``plain`` parts, the rest after."""
    first = iter(spread([w for w, need in zip(weights, needs, strict=True) if need], plain))
    rest = spread([w for w, need in zip(weights, needs, strict=True) if not need], parts - plain)
    after = iter(rest)
    return [next(first) if need else plain + next(after) for need in needs]


def cost(weights: list[int], places: list[int], needs: list[bool], parts: int) -> int:
    """What the heaviest part weighs, with ``COVERAGE`` when one of its slices needs it."""
    loads = [0] * parts
    covering = [False] * parts
    for weight, number, need in zip(weights, places, needs, strict=True):
        loads[number] += weight
        covering[number] = covering[number] or need
    return max(load + COVERAGE * has for load, has in zip(loads, covering, strict=True))


def covered(piece: Slice) -> bool:
    """Whether Stryker tests the mutants of the slice against the tests that cover them, so its
    part captures the coverage of each test: not for a slice of the suite nor an inert one."""
    cost = piece.weight
    return not (isinstance(cost, Cost) and (cost.suite or cost.inert))


def inert(slices: list[Slice]) -> bool:
    """Whether a part holds slices and none of them any code Stryker could mutate."""
    return bool(slices) and all(isinstance(p.weight, Cost) and p.weight.inert for p in slices)


def bare(slices: list[Slice]) -> bool:
    """Whether a part can skip the coverage of each test: none of its slices needs it."""
    return not any(covered(piece) for piece in slices)


def share(slices: list[Slice], part: Part) -> tuple[str, ...]:
    """The mutate patterns of one part: the slices of a file in a pattern of its own with one span
    each, and a whole file as its bare path."""
    mine = dealt(slices, part)
    files = dict.fromkeys(piece.file for piece in mine)
    return tuple(pattern(file, [piece for piece in mine if piece.file == file]) for file in files)


def pattern(file: str, pieces: list[Slice]) -> str:
    """The pattern for ``pieces`` of one ``file``: ``File.cs{0..120}{300..480}``; slices that touch
    are one span, and a file whole in the part is its bare path."""
    spans: list[list[int]] = []
    for piece in pieces:
        if spans and spans[-1][1] == piece.start:
            spans[-1][1] = piece.end
        else:
            spans.append([piece.start, piece.end])
    if spans == [[0, UNBOUNDED]]:
        return file
    return file + "".join(f"{{{start}..{end}}}" for start, end in spans)


def parts_of(count: int) -> tuple[Part | None, ...]:
    """The parts an area of ``count`` parts runs in; none to name when it is a single part."""
    return (None,) if count == 1 else tuple(Part(n, count) for n in range(1, count + 1))


def portion(parts: int, every: list[Slice], mine: list[Slice]) -> int:
    """How many parts the slices to mutate take: the share of the area's weight they hold of the
    parts its file asks for, rounded up, so a part weighs what it weighs in the whole run; never
    more parts than slices, and always one."""
    total = sum(piece.weight for piece in every)
    wanted = ceil(parts * sum(piece.weight for piece in mine) / total) if total else 1
    return max(1, min(wanted, len(mine)))
