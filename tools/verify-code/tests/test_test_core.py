"""Tests for test_core: environment, empty run, silent project, failure, skips, coverage."""

from __future__ import annotations

import sys
import tempfile
import unittest
from pathlib import Path

from steps import test_core
from steps.common import CommandOutcome, run_process

from . import support


class TestCoreTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)
        support.write(self.root, test_core.SOLUTION, support.SOLUTION_WITH_TESTS)
        self.results = self.root / ".verify-code" / test_core.RESULTS

    def runner(self, files: dict[str, str], ok: bool = True) -> support.FakeRunner:
        return support.FakeRunner(CommandOutcome(ok, "dotnet output"), files, self.results)

    def test_child_process_receives_the_telemetry_opt_out(self) -> None:
        script = "import os; print(os.environ['DOTNET_CLI_TELEMETRY_OPTOUT'])"

        outcome = run_process([sys.executable, "-c", script], self.root)

        self.assertTrue(outcome.ok, outcome.text)
        self.assertEqual(outcome.text.strip(), "1")

    def test_missing_solution_fails(self) -> None:
        (self.root / test_core.SOLUTION).unlink()

        result = test_core.run(support.context(self.root, self.runner({})))

        self.assertFalse(result.ok)
        self.assertIn("não há suíte", result.details[0])

    def test_no_report_written_fails_instead_of_passing_empty(self) -> None:
        result = test_core.run(support.context(self.root, self.runner({})))

        self.assertFalse(result.ok)
        self.assertIn(
            "nenhum relatório de teste foi escrito: o passo não passa vazio", result.details
        )

    def test_project_that_executed_no_test_fails_by_name(self) -> None:
        files = {
            "a.trx": support.trx("Alpha.Tests", 3, 0),
            "b.trx": support.trx("Beta.Tests", 0, 0),
        }

        result = test_core.run(support.context(self.root, self.runner(files)))

        self.assertFalse(result.ok)
        self.assertIn("o projeto de teste Beta.Tests não executou nenhum teste", result.details)

    def test_a_failed_test_fails_the_step(self) -> None:
        files = {
            "a.trx": support.trx("Alpha.Tests", 3, 1),
            "b.trx": support.trx("Beta.Tests", 2, 0),
        }

        result = test_core.run(support.context(self.root, self.runner(files, ok=False)))

        self.assertFalse(result.ok)
        self.assertIn("1 teste(s) do núcleo falharam", result.details)

    def test_every_project_ran_and_coverage_was_written_passes(self) -> None:
        files = {
            "a.trx": support.trx("Alpha.Tests", 3, 0),
            "b.trx": support.trx("Beta.Tests", 2, 0),
            "x.cobertura.xml": "<coverage />",
        }

        result = test_core.run(support.context(self.root, self.runner(files)))

        self.assertTrue(result.ok, result.details)
        self.assertIn("5 testes executados em 2 projetos", result.details)

    def test_skipped_tests_are_reported_with_their_reason(self) -> None:
        files = {
            "a.trx": support.trx("Alpha.Tests", 3, 0, skipped="ZQX-MOTIVO"),
            "b.trx": support.trx("Beta.Tests", 2, 0),
            "x.cobertura.xml": "<coverage />",
        }

        result = test_core.run(support.context(self.root, self.runner(files)))

        self.assertTrue(result.ok, result.details)
        self.assertIn("Alpha.Tests: 1 teste(s) saltado(s) (ZQX-MOTIVO)", result.details)

    def test_no_skip_line_when_nothing_was_skipped(self) -> None:
        files = {
            "a.trx": support.trx("Alpha.Tests", 3, 0),
            "b.trx": support.trx("Beta.Tests", 2, 0),
            "x.cobertura.xml": "<coverage />",
        }

        result = test_core.run(support.context(self.root, self.runner(files)))

        self.assertEqual(result.details, ("5 testes executados em 2 projetos",))

    def test_missing_coverage_report_fails(self) -> None:
        files = {
            "a.trx": support.trx("Alpha.Tests", 3, 0),
            "b.trx": support.trx("Beta.Tests", 2, 0),
        }

        result = test_core.run(support.context(self.root, self.runner(files)))

        self.assertFalse(result.ok)
        self.assertIn("nenhum relatório de cobertura Cobertura foi escrito", result.details)

    def test_dotnet_test_error_without_failed_test_still_fails(self) -> None:
        files = {
            "a.trx": support.trx("Alpha.Tests", 3, 0),
            "b.trx": support.trx("Beta.Tests", 2, 0),
            "x.cobertura.xml": "<coverage />",
        }

        result = test_core.run(support.context(self.root, self.runner(files, ok=False)))

        self.assertFalse(result.ok)
        self.assertIn("terminou com erro", result.details[0])

    def test_results_of_an_earlier_run_do_not_count(self) -> None:
        self.results.mkdir(parents=True)
        (self.results / "old.trx").write_text(support.trx("Alpha.Tests", 9, 0), encoding="utf-8")

        result = test_core.run(support.context(self.root, self.runner({})))

        self.assertFalse(result.ok)
        self.assertIn(
            "nenhum relatório de teste foi escrito: o passo não passa vazio", result.details
        )

    def test_the_command_asks_for_cobertura_and_trx_reports(self) -> None:
        runner = self.runner({})

        test_core.run(support.context(self.root, runner))

        self.assertIn("--coverage-output-format", runner.commands[0])
        self.assertIn("--report-xunit-trx", runner.commands[0])
