"""Tests for test_real_service: only the real-service trait, an empty run, failure and skips."""

from __future__ import annotations

import re
import tempfile
import unittest
from pathlib import Path

from steps import test_real_service
from steps.common import CommandOutcome

from . import support

WORKFLOW = support.VERIFY_CODE.parents[1] / ".github" / "workflows" / "verify-code.yml"


class TestRealServiceTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)
        support.write(self.root, test_real_service.SOLUTION, support.SOLUTION_WITH_TESTS)
        self.results = self.root / ".verify-code" / test_real_service.RESULTS

    def runner(self, files: dict[str, str], ok: bool = True) -> support.FakeRunner:
        return support.FakeRunner(CommandOutcome(ok, "dotnet output"), files, self.results)

    def test_missing_solution_fails(self) -> None:
        (self.root / test_real_service.SOLUTION).unlink()

        result = test_real_service.run(support.context(self.root, self.runner({})))

        self.assertFalse(result.ok)
        self.assertIn("não há suíte", result.details[0])

    def test_the_command_runs_only_the_real_service_trait(self) -> None:
        runner = self.runner({})

        test_real_service.run(support.context(self.root, runner))

        command = runner.commands[0]
        self.assertEqual("Category=RealService", command[command.index("--filter-trait") + 1])
        self.assertNotIn("--filter-not-trait", command)

    def test_no_test_executed_fails_instead_of_passing_empty(self) -> None:
        files = {"a.trx": support.trx("Alpha.Tests", 0, 0, skipped="ZQX-SEM-UV")}

        result = test_real_service.run(support.context(self.root, self.runner(files)))

        self.assertFalse(result.ok)
        self.assertIn("nenhum teste Category=RealService executou", result.details[0])
        self.assertIn("Alpha.Tests: 1 teste(s) saltado(s) (ZQX-SEM-UV)", result.details)

    def test_a_project_the_filter_left_empty_is_no_failure(self) -> None:
        files = {
            "a.trx": support.trx("Alpha.Tests", 2, 0),
            "b.trx": support.trx("Beta.Tests", 0, 0),
        }

        result = test_real_service.run(support.context(self.root, self.runner(files)))

        self.assertTrue(result.ok, result.details)
        self.assertEqual(("2 testes do serviço real executados",), result.details)

    def test_a_failed_test_fails_the_step(self) -> None:
        files = {"a.trx": support.trx("Alpha.Tests", 3, 1)}

        result = test_real_service.run(support.context(self.root, self.runner(files, ok=False)))

        self.assertFalse(result.ok)
        self.assertIn("1 teste(s) do serviço real falharam", result.details)

    def test_skipped_tests_are_listed_with_their_reason_when_the_step_passes(self) -> None:
        files = {"a.trx": support.trx("Alpha.Tests", 3, 0, skipped="ZQX-MOTIVO")}

        result = test_real_service.run(support.context(self.root, self.runner(files)))

        self.assertTrue(result.ok, result.details)
        self.assertIn("Alpha.Tests: 1 teste(s) saltado(s) (ZQX-MOTIVO)", result.details)

    def test_dotnet_error_without_failed_test_still_fails(self) -> None:
        files = {"a.trx": support.trx("Alpha.Tests", 3, 0)}

        result = test_real_service.run(support.context(self.root, self.runner(files, ok=False)))

        self.assertFalse(result.ok)
        self.assertIn("`dotnet test` terminou com erro sem teste reprovado", result.details)

    def test_results_of_an_earlier_run_do_not_count(self) -> None:
        self.results.mkdir(parents=True)
        (self.results / "old.trx").write_text(support.trx("Alpha.Tests", 9, 0), encoding="utf-8")

        result = test_real_service.run(support.context(self.root, self.runner({})))

        self.assertFalse(result.ok)


def job(name: str) -> str:
    """The text of one job of the workflow, up to the next job."""
    text = WORKFLOW.read_text(encoding="utf-8")
    start = text.index(f"\n  {name}:\n") + 1
    following = re.search(r"^  \w+:\n", text[start + 1 :], re.MULTILINE)
    return text[start : start + 1 + following.start()] if following else text[start:]


class RealServiceWorkflowTests(unittest.TestCase):
    def test_the_job_of_test_core_installs_no_uv(self) -> None:
        dotnet = job("dotnet")

        self.assertIn("--step build_core test_core", dotnet)
        self.assertNotIn("service-requirements.txt", dotnet)

    def test_the_real_service_job_builds_then_runs_the_step_with_uv_installed(self) -> None:
        real = job("real_service")

        self.assertIn("pip install -r tools/verify-code/service-requirements.txt", real)
        self.assertIn("--step build_core test_real_service", real)
