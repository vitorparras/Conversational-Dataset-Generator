"""Tests for the properties step: both languages must execute a property, or the step fails."""

from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from steps import properties
from steps.common import CommandOutcome

from . import support

PROPERTY_CLASS = "Core.Tests.Properties.Configuration.SomeInvariantsTests"
SERVICE_PROPERTY = "tests.properties.test_secret"
CORE_REPORT = "core.trx"


def trx(*tests: tuple[str, str]) -> str:
    """A TRX report with one result and one definition per (class name, outcome) pair."""
    results = "".join(
        f'<UnitTestResult testId="{i}" outcome="{outcome}" />'
        for i, (_, outcome) in enumerate(tests)
    )
    definitions = "".join(
        f'<UnitTest id="{i}"><TestMethod className="{name}" name="t" /></UnitTest>'
        for i, (name, _) in enumerate(tests)
    )
    return (
        '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">'
        f"<Results>{results}</Results><TestDefinitions>{definitions}</TestDefinitions></TestRun>"
    )


def junit(*cases: tuple[str, str]) -> str:
    """A JUnit report with one test case per (class name, child element) pair; "" is a pass."""
    body = "".join(
        f'<testcase classname="{name}" name="t">{f"<{child} />" if child else ""}</testcase>'
        for name, child in cases
    )
    return f'<testsuites><testsuite name="pytest">{body}</testsuite></testsuites>'


EMPTY_TRX = trx()
EMPTY_JUNIT = junit()


class PropertiesTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)
        support.write(self.root, properties.CORE_PROJECT, "<Project />")
        self.results = self.root / ".verify-code" / properties.RESULTS

    def run_step(
        self, core: str | None, service: str | None, ok: bool = True
    ) -> tuple[bool, tuple[str, ...], support.FakeRunner]:
        files = {CORE_REPORT: core} if core is not None else {}
        files |= {properties.JUNIT: service} if service is not None else {}
        runner = support.FakeRunner(CommandOutcome(ok, "output"), files, self.results)
        result = properties.run(support.context(self.root, runner))
        return result.ok, result.details, runner

    def test_both_sides_with_a_property_pass(self) -> None:
        ok, details, _ = self.run_step(
            trx((PROPERTY_CLASS, "Passed")), junit((SERVICE_PROPERTY, ""))
        )

        self.assertTrue(ok, details)
        self.assertIn("1 propriedades em núcleo (.NET)", details)
        self.assertIn("1 propriedades em serviço (Python)", details)

    def test_a_missing_core_project_fails(self) -> None:
        (self.root / properties.CORE_PROJECT).unlink()

        ok, details, _ = self.run_step(None, None)

        self.assertFalse(ok)
        self.assertIn("não há propriedades", details[0])

    def test_a_service_run_that_selected_no_property_fails_naming_the_service(self) -> None:
        ok, details, _ = self.run_step(trx((PROPERTY_CLASS, "Passed")), EMPTY_JUNIT)

        self.assertFalse(ok)
        self.assertTrue(details[0].startswith("serviço (Python): nenhuma propriedade executada"))

    def test_a_core_run_that_selected_no_property_fails_naming_the_core(self) -> None:
        ok, details, _ = self.run_step(EMPTY_TRX, junit((SERVICE_PROPERTY, "")))

        self.assertFalse(ok)
        self.assertTrue(details[0].startswith("núcleo (.NET): nenhuma propriedade executada"))

    def test_both_sides_without_a_report_are_named(self) -> None:
        ok, details, _ = self.run_step(None, None)

        self.assertFalse(ok)
        sides = [d.split(":")[0] for d in details if d.endswith("o passo não passa vazio")]
        self.assertEqual(sides, ["núcleo (.NET)", "serviço (Python)"])

    def test_a_skipped_service_property_did_not_run(self) -> None:
        ok, details, _ = self.run_step(
            trx((PROPERTY_CLASS, "Passed")), junit((SERVICE_PROPERTY, "skipped"))
        )

        self.assertFalse(ok)
        self.assertTrue(details[0].startswith("serviço (Python)"))

    def test_a_not_executed_core_property_did_not_run(self) -> None:
        ok, details, _ = self.run_step(
            trx((PROPERTY_CLASS, "NotExecuted")), junit((SERVICE_PROPERTY, ""))
        )

        self.assertFalse(ok)
        self.assertTrue(details[0].startswith("núcleo (.NET)"))

    def test_a_failed_service_property_fails_the_step(self) -> None:
        ok, details, _ = self.run_step(
            trx((PROPERTY_CLASS, "Passed")), junit((SERVICE_PROPERTY, "failure")), ok=False
        )

        self.assertFalse(ok)
        self.assertIn("serviço (Python): 1 propriedade(s) falharam", details)

    def test_a_failed_core_property_fails_the_step(self) -> None:
        ok, details, _ = self.run_step(
            trx((PROPERTY_CLASS, "Failed")), junit((SERVICE_PROPERTY, "")), ok=False
        )

        self.assertFalse(ok)
        self.assertIn("núcleo (.NET): 1 propriedade(s) falharam", details)

    def test_a_command_error_without_a_failed_property_still_fails(self) -> None:
        ok, details, _ = self.run_step(
            trx((PROPERTY_CLASS, "Passed")), junit((SERVICE_PROPERTY, "")), ok=False
        )

        self.assertFalse(ok)
        self.assertIn("terminou com erro", details[0])

    def test_results_of_an_earlier_run_do_not_count(self) -> None:
        self.results.mkdir(parents=True)
        (self.results / CORE_REPORT).write_text(trx((PROPERTY_CLASS, "Passed")), encoding="utf-8")
        (self.results / properties.JUNIT).write_text(
            junit((SERVICE_PROPERTY, "")), encoding="utf-8"
        )

        ok, _, _ = self.run_step(None, None)

        self.assertFalse(ok)

    def test_each_side_runs_only_what_is_marked_as_a_property(self) -> None:
        _, _, runner = self.run_step(trx((PROPERTY_CLASS, "Passed")), junit((SERVICE_PROPERTY, "")))

        self.assertEqual(
            runner.commands[0][:4], ["dotnet", "test", "--project", properties.CORE_PROJECT]
        )
        self.assertIn("Kind=Property", runner.commands[0])
        self.assertEqual(runner.commands[1][5:8], ["pytest", "-m", "property"])
