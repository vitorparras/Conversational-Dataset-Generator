"""Tests for the configuration of a part that skips the coverage of each test (#935): Stryker tests
each mutant of a static initializer against the whole suite anyway, so a part holding only those
needs no coverage per test, and turning it off judges every mutant as before."""

from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

from steps.mutation_members import slices
from steps.stryker_config import configuration

from . import support

PROJECT = "Core.X"
LIST = """namespace Core.X;

public static class Columns
{
    private const string Table = "t";

    public static IReadOnlyList<string> All { get; } =
    [
        "a",
        "b",
    ];

    public static int Count(int n)
    {
        return n + 1;
    }
}
"""


class BareConfigurationTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)
        support.write(self.root, f"core/src/{PROJECT}/Columns.cs", LIST)
        self.tests = (self.root / "core/tests/Core.X.Tests/Core.X.Tests.csproj",)
        found = slices(LIST)
        self.elements = [(a, b) for a, b, w in found if getattr(w, "suite", False)]
        self.method = next((a, b) for a, b, _ in found if "Count" in LIST[a:b])

    def options(self, *patterns: str) -> dict[str, object]:
        text = configuration(PROJECT, patterns, self.tests)
        return json.loads(text)["stryker-config"]

    def test_a_part_of_only_slices_of_the_suite_turns_the_coverage_of_each_test_off(self) -> None:
        spans = "".join(f"{{{a}..{b}}}" for a, b in self.elements)

        found = self.options(f"Columns.cs{spans}")

        self.assertEqual(found["coverage-analysis"], "off")

    def test_a_part_with_a_slice_outside_the_suite_keeps_the_coverage(self) -> None:
        (a, b), (c, d) = self.elements[0], self.method

        found = self.options(f"Columns.cs{{{a}..{b}}}{{{c}..{d}}}")

        self.assertNotIn("coverage-analysis", found)

    def test_a_glob_or_a_file_that_is_not_there_keeps_the_coverage(self) -> None:
        for patterns in (("**/*.cs",), ("**/*.cs", "!Other/**/*.cs"), ("Missing.cs{0..10}",)):
            self.assertNotIn("coverage-analysis", self.options(*patterns), patterns)

    def test_the_rest_of_the_configuration_does_not_change(self) -> None:
        a, b = self.elements[0]

        found = self.options(f"Columns.cs{{{a}..{b}}}")

        self.assertEqual(found["mutate"], [f"Columns.cs{{{a}..{b}}}"])
        self.assertEqual((found["test-runner"], found["concurrency"]), ("mtp", 1))
        self.assertEqual(found["thresholds"], {"high": 100, "low": 0, "break": 0})


if __name__ == "__main__":
    unittest.main()
