"""Tests for the test_tools step, run for real against a synthetic tools/ directory."""

from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from steps import test_tools
from steps.common import CommandOutcome

from . import support

SERVICE_PROJECT = support.VERIFY_CODE.parents[1] / "language-service" / "pyproject.toml"
REQUIREMENTS = support.VERIFY_CODE / "lint-requirements.txt"
RAN = CommandOutcome(True, "Ran 3 tests in 0.1s\n\nOK\n")

PASSING_TEST = (
    "import unittest\n\nclass T(unittest.TestCase):\n    def test_ok(self) -> None:\n        pass\n"
)
FAILING_TEST = PASSING_TEST.replace(
    "def test_ok(self) -> None:\n        pass", "def test_no(self) -> None:\n        self.fail()"
)


class TestToolsTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)
        (self.root / test_tools.TOOLS).mkdir()

    def test_missing_tools_directory_fails(self) -> None:
        (self.root / test_tools.TOOLS).rmdir()

        result = test_tools.run(support.context(self.root))

        self.assertFalse(result.ok)
        self.assertIn("não há suíte", result.details[0])

    def test_directory_without_tests_fails_instead_of_passing_empty(self) -> None:
        result = test_tools.run(support.context(self.root))

        self.assertFalse(result.ok)
        self.assertIn("nenhum teste encontrado em tools/: o passo não passa vazio", result.details)

    def test_a_passing_test_passes_with_its_count(self) -> None:
        support.write(self.root, "tools/test_sample.py", PASSING_TEST)

        result = test_tools.run(support.context(self.root))

        self.assertTrue(result.ok, result.details)
        self.assertIn("1 testes de tools/ executados", result.details)

    def test_a_failing_test_fails_the_step(self) -> None:
        support.write(self.root, "tools/test_sample.py", FAILING_TEST)

        result = test_tools.run(support.context(self.root))

        self.assertFalse(result.ok)
        self.assertIn("testes de tools/ falharam", result.details)


class MeasuredToolsTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)
        self.results = self.root / ".verify-code" / test_tools.RESULTS

    def runner(self, files: dict[str, str]) -> support.FakeRunner:
        return support.FakeRunner(RAN, files, self.results)

    def test_measured_tests_run_under_coverage_of_tools_without_their_tests(self) -> None:
        runner = self.runner({})

        test_tools.run_suite(support.context(self.root, runner), measured=True)

        self.assertEqual(runner.commands[0][1:4], ["-m", "coverage", "run"])
        self.assertIn("--source", runner.commands[0])
        self.assertIn("*/tests/*", runner.commands[0])
        self.assertEqual(
            runner.commands[0][-6:], ["unittest", "discover", "-s", "tools", "-p", "test_*.py"]
        )

    def test_measured_tests_without_xml_report_fail(self) -> None:
        result = test_tools.run_suite(support.context(self.root, self.runner({})), measured=True)

        self.assertFalse(result.ok)
        self.assertIn("nenhum relatório de cobertura XML foi escrito", result.details)

    def test_measured_tests_with_xml_report_pass_naming_it(self) -> None:
        runner = self.runner({test_tools.REPORT: "<coverage />"})

        result = test_tools.run_suite(support.context(self.root, runner), measured=True)

        self.assertTrue(result.ok, result.details)
        self.assertIn("cobertura em .verify-code/tools/coverage.xml", result.details)

    def test_unmeasured_tests_pass_saying_how_to_measure(self) -> None:
        runner = self.runner({})

        result = test_tools.run_suite(support.context(self.root, runner), measured=False)

        self.assertTrue(result.ok, result.details)
        self.assertIn("cobertura não medida", result.details[1])
        self.assertEqual(len(runner.commands), 1)

    def test_a_report_of_an_earlier_run_is_discarded_when_not_measured(self) -> None:
        support.write(self.results, test_tools.REPORT, "<coverage />")

        test_tools.run_suite(support.context(self.root, self.runner({})), measured=False)

        self.assertFalse((self.results / test_tools.REPORT).exists())

    def test_the_coverage_pin_of_the_linters_matches_the_service(self) -> None:
        pin = "coverage==7.16.2"

        self.assertIn(f'"{pin}"', SERVICE_PROJECT.read_text(encoding="utf-8"))
        self.assertIn(pin, REQUIREMENTS.read_text(encoding="utf-8").splitlines())
