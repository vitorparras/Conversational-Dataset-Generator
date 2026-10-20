"""Tests for the readers of the Stryker.NET JSON report and of what mutmut leaves on disk."""

from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from steps import mutmut_report, stryker_report
from steps.mutant import Mutant, MutantStatus
from steps.mutmut_report import Entry

from . import support
from .mutation_samples import SHOWN, SOURCE, meta
from .mutation_samples import stryker_report as stryker_report_text

SURVIVOR = Entry("language_service.auth.xǁVerifierǁaccepts__mutmut_2", "src/a/auth.py", 0)


class StrykerReportTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)

    def read(self, *statuses: str) -> tuple[Mutant, ...]:
        file = str(self.root / "core/src/P/Logging/F.cs")
        support.write(self.root, "r.json", stryker_report_text(file, *statuses))
        return stryker_report.read_stryker(self.root / "r.json", self.root)

    def test_a_survivor_keeps_its_file_from_the_root_its_line_and_its_change(self) -> None:
        mutants = self.read("Survived")

        expected = Mutant(
            "core/src/P/Logging/F.cs", 10, "Equality mutation: `a != b`", MutantStatus.SURVIVED
        )
        self.assertEqual(mutants, (expected,))

    def test_a_mutant_no_test_covers_counts_as_a_survivor(self) -> None:
        mutants = self.read("NoCoverage")

        self.assertEqual(mutants[0].status, MutantStatus.SURVIVED)

    def test_a_timeout_counts_as_killed(self) -> None:
        mutants = self.read("Timeout")

        self.assertEqual(mutants[0].status, MutantStatus.KILLED)

    def test_a_mutant_that_does_not_compile_is_not_counted(self) -> None:
        mutants = self.read("CompileError")

        self.assertEqual(mutants[0].status, MutantStatus.NOT_COUNTED)

    def test_a_pending_mutant_is_unchecked(self) -> None:
        mutants = self.read("Pending")

        self.assertEqual(mutants[0].status, MutantStatus.UNCHECKED)


class MutmutReportTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)

    def test_entries_come_from_every_meta_file_with_their_source_file(self) -> None:
        support.write(self.root, "mutants/src/a/auth.py.meta", meta({"a.auth.x_f__mutmut_1": 1}))

        found = mutmut_report.entries(self.root)

        self.assertEqual(found, (Entry("a.auth.x_f__mutmut_1", "src/a/auth.py", 1),))

    def test_a_passing_run_is_a_survivor(self) -> None:
        self.assertEqual(mutmut_report.status_of(0), MutantStatus.SURVIVED)

    def test_a_mutant_no_test_reached_is_a_survivor(self) -> None:
        self.assertEqual(mutmut_report.status_of(33), MutantStatus.SURVIVED)

    def test_a_failing_run_is_killed(self) -> None:
        self.assertEqual(mutmut_report.status_of(1), MutantStatus.KILLED)

    def test_a_timeout_is_killed(self) -> None:
        self.assertEqual(mutmut_report.status_of(36), MutantStatus.KILLED)

    def test_a_mutant_never_checked_is_unchecked(self) -> None:
        self.assertEqual(mutmut_report.status_of(None), MutantStatus.UNCHECKED)

    def test_the_function_of_a_method_mutant_is_its_last_name(self) -> None:
        self.assertEqual(mutmut_report.function_of(SURVIVOR.name), "accepts")

    def test_the_function_of_a_module_level_mutant_drops_the_prefix(self) -> None:
        self.assertEqual(mutmut_report.function_of("a.b.x_verify__mutmut_12"), "verify")

    def test_a_survivor_is_placed_on_the_source_line_the_diff_removes(self) -> None:
        source = "x = 1\n\n" + SOURCE

        found = mutmut_report.survivor(SURVIVOR, SHOWN, source, "svc")

        change = "`return given == self.secret` → `return given != self.secret`"
        self.assertEqual(found, Mutant("svc/src/a/auth.py", 4, change, MutantStatus.SURVIVED))

    def test_the_same_line_in_an_earlier_function_is_not_taken(self) -> None:
        source = "def other(self, given):\n    return given == self.secret\n\n" + SOURCE

        found = mutmut_report.survivor(SURVIVOR, SHOWN, source, "svc")

        self.assertEqual(found.line, 5)
