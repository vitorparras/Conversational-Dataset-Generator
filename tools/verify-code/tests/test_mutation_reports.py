"""Tests for the readers of the Stryker.NET JSON report and of what mutmut leaves on disk."""

from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

from steps import mutmut_report, stryker_report
from steps.mutant import Mutant, MutantStatus
from steps.mutmut_report import Entry

from . import support
from .mutation_samples import (
    AWAITED,
    SHOWN,
    SOURCE,
    awaited_pair,
    meta,
    stryker_mutant,
    stryker_span,
    stryker_with_source,
)
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

    def test_a_mutant_switched_off_is_ignored_neither_killed_nor_survived(self) -> None:
        mutants = self.read("Ignored")

        self.assertEqual(mutants[0].status, MutantStatus.IGNORED)

    def static_report(self, status: str) -> tuple[Mutant, ...]:
        mutant = {**stryker_mutant(10, status), "static": True}
        file = str(self.root / "core/src/P/Logging/F.cs")
        report = json.dumps({"schemaVersion": "2", "files": {file: {"mutants": [mutant]}}})
        support.write(self.root, "s.json", report)
        return stryker_report.read_stryker(self.root / "s.json", self.root)

    def test_a_static_mutant_is_static_whether_stryker_killed_it_or_not(self) -> None:
        for status in ("Killed", "Survived", "NoCoverage", "Timeout"):
            with self.subTest(status):
                self.assertEqual(self.static_report(status)[0].status, MutantStatus.STATIC)

    def test_a_static_mutant_that_never_ran_keeps_its_status(self) -> None:
        self.assertEqual(self.static_report("CompileError")[0].status, MutantStatus.NOT_COUNTED)
        self.assertEqual(self.static_report("Ignored")[0].status, MutantStatus.IGNORED)

    def test_a_pending_mutant_is_unchecked(self) -> None:
        mutants = self.read("Pending")

        self.assertEqual(mutants[0].status, MutantStatus.UNCHECKED)

    def read_source(
        self, source: str, *mutants: dict[str, object], argument_of: str | None = "ConfigureAwait"
    ) -> tuple[Mutant, ...]:
        file = str(self.root / "core/src/P/F.cs")
        support.write(self.root, "r.json", stryker_with_source(file, source, *mutants))
        return stryker_report.read_stryker(self.root / "r.json", self.root, argument_of)

    def test_the_false_argument_of_the_method_is_ignored_even_when_it_survived(self) -> None:
        _, argument = awaited_pair("Survived")

        mutants = self.read_source(AWAITED, argument)

        self.assertEqual(mutants[0].status, MutantStatus.IGNORED)

    def test_a_statement_mutation_on_the_line_of_the_call_still_counts(self) -> None:
        statement, argument = awaited_pair("Survived")

        mutants = self.read_source(AWAITED, statement, argument)

        self.assertEqual([m.status for m in mutants], [MutantStatus.SURVIVED, MutantStatus.IGNORED])

    def test_a_killed_statement_mutation_on_the_line_of_the_call_stays_killed(self) -> None:
        statement, _ = awaited_pair("Killed")

        mutants = self.read_source(AWAITED, statement)

        self.assertEqual(mutants[0].status, MutantStatus.KILLED)

    def test_the_argument_of_a_using_statement_is_ignored(self) -> None:
        source = "        await using (connection.ConfigureAwait(false))\n"
        column = source.index("false") + 1
        argument = stryker_span("Boolean mutation", "true", (1, column, column + 5), "Survived")

        mutants = self.read_source(source, argument)

        self.assertEqual(mutants[0].status, MutantStatus.IGNORED)

    def test_a_false_argument_of_another_method_counts(self) -> None:
        source = "        Toggle(false);\n"
        column = source.index("false") + 1
        argument = stryker_span("Boolean mutation", "true", (1, column, column + 5), "Survived")

        mutants = self.read_source(source, argument)

        self.assertEqual(mutants[0].status, MutantStatus.SURVIVED)

    def test_another_mutation_of_the_argument_span_counts(self) -> None:
        column = AWAITED.index("false") + 1
        other = stryker_span("String mutation", '""', (1, column, column + 5), "Survived")

        mutants = self.read_source(AWAITED, other)

        self.assertEqual(mutants[0].status, MutantStatus.SURVIVED)

    def test_a_true_argument_counts(self) -> None:
        source = AWAITED.replace("false", "true")
        column = source.index("true") + 1
        argument = stryker_span("Boolean mutation", "false", (1, column, column + 4), "Survived")

        mutants = self.read_source(source, argument)

        self.assertEqual(mutants[0].status, MutantStatus.SURVIVED)

    def test_without_a_method_to_skip_the_false_argument_counts(self) -> None:
        _, argument = awaited_pair("Survived")

        mutants = self.read_source(AWAITED, argument, argument_of=None)

        self.assertEqual(mutants[0].status, MutantStatus.SURVIVED)

    def test_another_method_name_does_not_skip_the_argument(self) -> None:
        _, argument = awaited_pair("Survived")

        mutants = self.read_source(AWAITED, argument, argument_of="Other")

        self.assertEqual(mutants[0].status, MutantStatus.SURVIVED)

    def test_a_mutant_past_the_end_of_the_source_counts(self) -> None:
        _, argument = awaited_pair("Survived")

        mutants = self.read_source("", argument)

        self.assertEqual(mutants[0].status, MutantStatus.SURVIVED)


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

    def test_the_scope_of_a_method_mutant_is_its_class_and_its_name(self) -> None:
        self.assertEqual(mutmut_report.scope_of(SURVIVOR.name), ("Verifier", "accepts"))

    def test_the_scope_of_a_module_level_mutant_drops_the_prefix(self) -> None:
        self.assertEqual(mutmut_report.scope_of("a.b.x_verify__mutmut_12"), ("verify",))

    def test_a_method_named_as_one_of_an_earlier_class_is_found_in_its_own_class(self) -> None:
        source = SOURCE.replace("Verifier", "Other") + SOURCE

        found = mutmut_report.survivor(SURVIVOR, SHOWN, source, "svc")

        self.assertEqual(found.line, 6)

    def test_a_diff_that_could_not_be_read_leaves_the_survivor_unplaced_and_says_so(self) -> None:
        spaced = SOURCE.replace("        return", "\n        return")

        found = mutmut_report.survivor(SURVIVOR, "", spaced, "svc")

        self.assertEqual((found.line, found.change), (0, mutmut_report.UNREAD))

    def test_a_survivor_is_placed_on_the_source_line_the_diff_removes(self) -> None:
        source = "x = 1\n\n" + SOURCE

        found = mutmut_report.survivor(SURVIVOR, SHOWN, source, "svc")

        change = "`return given == self.secret` → `return given != self.secret`"
        self.assertEqual(found, Mutant("svc/src/a/auth.py", 5, change, MutantStatus.SURVIVED))

    def test_the_same_line_in_an_earlier_function_is_not_taken(self) -> None:
        source = "def other(self, given):\n    return given == self.secret\n\n" + SOURCE

        found = mutmut_report.survivor(SURVIVOR, SHOWN, source, "svc")

        self.assertEqual(found.line, 6)
