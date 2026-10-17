"""Tests for the test_service step: empty run, skipped-only run, failure, coverage, commands."""

from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from steps import build_service, test_service
from steps.common import CommandOutcome

from . import support


class TestServiceTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)
        support.write(self.root, build_service.PROJECT, support.PYPROJECT_WITH_TESTS)
        self.results = self.root / ".verify-code" / test_service.RESULTS

    def runner(self, files: dict[str, str], ok: bool = True) -> support.FakeRunner:
        return support.FakeRunner(CommandOutcome(ok, "pytest output"), files, self.results)

    def test_missing_project_fails(self) -> None:
        (self.root / build_service.PROJECT).unlink()

        result = test_service.run(support.context(self.root, self.runner({})))

        self.assertFalse(result.ok)
        self.assertIn("não há suíte", result.details[0])

    def test_no_report_written_fails_instead_of_passing_empty(self) -> None:
        result = test_service.run(support.context(self.root, self.runner({})))

        self.assertFalse(result.ok)
        self.assertIn(
            "nenhum relatório de teste foi escrito: o passo não passa vazio", result.details
        )

    def test_a_report_with_zero_tests_fails(self) -> None:
        files = {test_service.JUNIT: support.junit(0, 0)}

        result = test_service.run(support.context(self.root, self.runner(files)))

        self.assertFalse(result.ok)
        self.assertIn("nenhum teste de language-service/ executado", result.details[0])

    def test_a_report_where_every_test_was_skipped_fails(self) -> None:
        files = {test_service.JUNIT: support.junit(3, 0, skipped=3)}

        result = test_service.run(support.context(self.root, self.runner(files)))

        self.assertFalse(result.ok)
        self.assertIn("nenhum teste de language-service/ executado", result.details[0])

    def test_a_failed_test_fails_the_step(self) -> None:
        files = {test_service.JUNIT: support.junit(5, 1)}

        result = test_service.run(support.context(self.root, self.runner(files, ok=False)))

        self.assertFalse(result.ok)
        self.assertIn("1 teste(s) do serviço falharam", result.details)

    def test_pytest_error_without_failed_test_still_fails(self) -> None:
        files = {test_service.JUNIT: support.junit(5, 0), test_service.REPORT: "<coverage />"}

        result = test_service.run(support.context(self.root, self.runner(files, ok=False)))

        self.assertFalse(result.ok)
        self.assertIn("terminou com erro", result.details[0])

    def test_missing_coverage_report_fails(self) -> None:
        files = {test_service.JUNIT: support.junit(5, 0)}

        result = test_service.run(support.context(self.root, self.runner(files)))

        self.assertFalse(result.ok)
        self.assertIn("nenhum relatório de cobertura XML foi escrito", result.details)

    def test_tests_ran_and_coverage_was_written_passes(self) -> None:
        files = {test_service.JUNIT: support.junit(5, 0), test_service.REPORT: "<coverage />"}

        result = test_service.run(support.context(self.root, self.runner(files)))

        self.assertTrue(result.ok, result.details)
        self.assertIn("5 testes de language-service/ executados", result.details)

    def test_results_of_an_earlier_run_do_not_count(self) -> None:
        self.results.mkdir(parents=True)
        (self.results / test_service.JUNIT).write_text(support.junit(9, 0), encoding="utf-8")

        result = test_service.run(support.context(self.root, self.runner({})))

        self.assertFalse(result.ok)
        self.assertIn(
            "nenhum relatório de teste foi escrito: o passo não passa vazio", result.details
        )

    def test_pytest_runs_under_coverage_inside_the_locked_service_environment(self) -> None:
        runner = self.runner({})

        test_service.run(support.context(self.root, runner))

        self.assertEqual(
            runner.commands[0][:7],
            ["uv", "run", "--locked", "--directory", "language-service", "coverage", "run"],
        )
        self.assertIn("--junitxml", runner.commands[0])
        self.assertIn("pytest", runner.commands[0])

    def test_coverage_xml_is_asked_after_the_tests_pass(self) -> None:
        files = {test_service.JUNIT: support.junit(5, 0), test_service.REPORT: "<coverage />"}
        runner = self.runner(files)

        test_service.run(support.context(self.root, runner))

        self.assertEqual(runner.commands[1][5:7], ["coverage", "xml"])
        self.assertIn(str(self.results / test_service.REPORT), runner.commands[1])
