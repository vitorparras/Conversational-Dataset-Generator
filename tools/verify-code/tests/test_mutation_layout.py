"""Tests for the parts that skip the coverage of each test (#935): the slices whose mutants Stryker
tests against the whole suite gather in parts of their own, apart from those that need it, when that
spares the heaviest part more than a mutant of the suite."""

from __future__ import annotations

import unittest

from steps import mutation_parts
from steps.mutation_cost import Cost
from steps.mutation_parts import Part, Slice


def suite(count: int, first: int = 0) -> list[Slice]:
    """``count`` slices of the suite of a literal each, from slice ``first`` of ``s.cs``."""
    return [Slice("s.cs", n * 10, n * 10 + 10, Cost(200, suite=True)) for n in range(first, count)]


def plain(count: int, weight: int) -> list[Slice]:
    """``count`` slices of ``p.cs`` of ``weight`` tokens each, which need the coverage."""
    return [Slice("p.cs", n * 10, n * 10 + 10, Cost(weight)) for n in range(count)]


def kinds(slices: list[Slice], of: int) -> list[list[str]]:
    """The files of the slices of each of ``of`` parts."""
    return [[p.file for p in mutation_parts.dealt(slices, Part(n, of))] for n in range(1, of + 1)]


class LayoutTests(unittest.TestCase):
    def test_the_slices_of_the_suite_gather_apart_when_that_spares_the_heaviest_part(self) -> None:
        slices = suite(8) + plain(6, 100)

        found = kinds(slices, 4)

        self.assertEqual(found, [["p.cs"] * 3, ["p.cs"] * 3, ["s.cs"] * 4, ["s.cs"] * 4])

    def test_a_separation_that_spares_no_more_than_a_mutant_leaves_the_lightest_part(self) -> None:
        slices = suite(3) + plain(1, 10)

        found = kinds(slices, 3)

        self.assertEqual(found, [["p.cs", "s.cs"], ["s.cs"], ["s.cs"]])

    def test_every_slice_is_in_exactly_one_part_when_they_gather_apart(self) -> None:
        slices = suite(30) + plain(9, 70)

        for count in range(1, 12):
            dealt = [
                p for n in range(1, count + 1) for p in mutation_parts.dealt(slices, Part(n, count))
            ]

            self.assertEqual(sorted(dealt), sorted(slices), count)

    def test_an_inert_slice_goes_with_the_suite_and_leaves_the_part_bare(self) -> None:
        slices = [*suite(2), Slice("i.cs", 0, 5, Cost(3, inert=True))]

        self.assertTrue(mutation_parts.bare(slices))
        self.assertFalse(mutation_parts.bare([*suite(2), *plain(1, 3)]))
        self.assertFalse(mutation_parts.bare([Slice("a.cs", 0, 10, 4)]))

    def test_a_part_is_inert_only_with_slices_and_none_holding_code(self) -> None:
        still = Slice("i.cs", 0, 5, Cost(3, inert=True))

        self.assertTrue(mutation_parts.inert([still, still]))
        self.assertFalse(mutation_parts.inert([]))
        self.assertFalse(mutation_parts.inert([still, *suite(1)]))
        self.assertFalse(mutation_parts.inert([Slice("a.cs", 0, 10, 4)]))


if __name__ == "__main__":
    unittest.main()
