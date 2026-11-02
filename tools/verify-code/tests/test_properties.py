"""Tests for the properties step: both languages must execute a property, or the step fails."""

from __future__ import annotations

import tempfile
import unittest
from pathlib import Path
from xml.sax.saxutils import escape

from steps import properties
from steps.build_core import SOLUTION
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


def sides_without_property(details: tuple[str, ...]) -> list[str]:
    """The sides a failed step names as having executed no property."""
    named = (d for d in details if d.endswith("o passo não passa vazio"))
    return [d.split(":")[0] for d in named]


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
        support.write(self.root, SOLUTION, "<Solution />")
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
        (self.root / SOLUTION).unlink()

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
        self.assertEqual(sides_without_property(details), ["núcleo (.NET)", "serviço (Python)"])

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

        self.assertEqual(runner.commands[0][:3], ["dotnet", "test", SOLUTION])
        self.assertIn("Kind=Property", runner.commands[0])
        self.assertEqual(runner.commands[1][5:8], ["pytest", "-m", "property"])

    def test_the_core_side_runs_the_solution_and_not_a_fixed_test_project(self) -> None:
        _, _, runner = self.run_step(trx((PROPERTY_CLASS, "Passed")), junit((SERVICE_PROPERTY, "")))

        self.assertNotIn("--project", runner.commands[0])

    def test_a_test_project_without_a_property_does_not_fail_the_side(self) -> None:
        _, _, runner = self.run_step(trx((PROPERTY_CLASS, "Passed")), junit((SERVICE_PROPERTY, "")))

        command = runner.commands[0]
        self.assertEqual(command[command.index("--ignore-exit-code") + 1], "8")

    def test_properties_of_several_test_projects_count_together(self) -> None:
        reports = {
            "boundary.trx": trx(("Core.Boundary.Tests.AnswerInvariants", "Passed")),
            CORE_REPORT: trx((PROPERTY_CLASS, "Passed")),
            properties.JUNIT: junit((SERVICE_PROPERTY, "")),
        }
        runner = support.FakeRunner(CommandOutcome(True, "output"), reports, self.results)

        result = properties.run(support.context(self.root, runner))

        self.assertIn("2 propriedades em núcleo (.NET)", result.details)


SAMPLE_PROPERTY = (
    "Core.Persistence.Tests.Custody.CustodyChainPropertyTests.NoLinkEndsWithTwoSuccessors"
)
SAMPLE_SEED = "00000F3Qi9d7"
# a property that broke on a loaded run: the header says only that it broke, the cause is below
SAMPLE_MESSAGE = (
    f'CsCheck.CsCheckException : Set seed: "{SAMPLE_SEED}" or -e CsCheck_Seed={SAMPLE_SEED} '
    "to reproduce (1 shrinks, 0 skipped, 12 total).\n"
    "WriterPlan { Writers = <>z__ReadOnlyList }\n"
    "---- Npgsql.NpgsqlException : The operation has timed out\n"
    "-------- System.TimeoutException : The operation has timed out."
)


def trx_failing(name: str, message: str | None) -> str:
    """A TRX report with one broken test, named and with the message the runner writes for it."""
    error = (
        ""
        if message is None
        else f"<Output><ErrorInfo><Message>{escape(message)}</Message></ErrorInfo></Output>"
    )
    return (
        '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results>'
        f'<UnitTestResult testId="0" testName="{name}" outcome="Failed">'
        f"{error}</UnitTestResult></Results></TestRun>"
    )


class FailedPropertyIsNamedTests(unittest.TestCase):
    """A broken property is named with its message and seed, so a run that is gone can be told."""

    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)
        support.write(self.root, SOLUTION, "<Solution />")
        self.results = self.root / ".verify-code" / properties.RESULTS

    def details(self, core: str, service: str = junit((SERVICE_PROPERTY, ""))) -> tuple[str, ...]:
        reports = {CORE_REPORT: core, properties.JUNIT: service}
        runner = support.FakeRunner(CommandOutcome(False, "output"), reports, self.results)
        return properties.run(support.context(self.root, runner)).details

    def test_the_name_the_first_line_the_cause_and_the_seed_are_printed(self) -> None:
        details = self.details(trx_failing(SAMPLE_PROPERTY, SAMPLE_MESSAGE))

        self.assertIn(f"  propriedade reprovada: {SAMPLE_PROPERTY}", details)
        self.assertIn(f"    mensagem: {SAMPLE_MESSAGE.splitlines()[0]}", details)
        self.assertIn("    causa: Npgsql.NpgsqlException : The operation has timed out", details)
        self.assertIn(f"    CsCheck_Seed={SAMPLE_SEED}", details)

    def test_the_lines_come_after_the_count_and_before_the_output_of_the_command(self) -> None:
        details = self.details(trx_failing(SAMPLE_PROPERTY, SAMPLE_MESSAGE))

        self.assertEqual(details[0], "núcleo (.NET): 1 propriedade(s) falharam")
        self.assertEqual(details[-1], "  | output")

    def test_a_message_without_seed_or_cause_prints_only_the_name_and_the_first_line(self) -> None:
        details = self.details(trx_failing(SAMPLE_PROPERTY, "Assert.Equal() Failure\nExpected: 1"))

        self.assertEqual(
            details[1:4],
            (
                f"  propriedade reprovada: {SAMPLE_PROPERTY}",
                "    mensagem: Assert.Equal() Failure",
                "  | output",
            ),
        )

    def test_a_report_without_a_message_still_names_the_property(self) -> None:
        details = self.details(trx_failing(SAMPLE_PROPERTY, None))

        self.assertIn(f"  propriedade reprovada: {SAMPLE_PROPERTY}", details)
        self.assertIn("    mensagem: o relatório não traz mensagem", details)

    def test_every_broken_property_of_the_report_is_named(self) -> None:
        both = trx_failing("A.First", "one").replace(
            "</Results>",
            trx_failing("B.Second", "two").split("<Results>")[1].split("</Results>")[0]
            + "</Results>",
        )

        details = self.details(both)

        self.assertIn("  propriedade reprovada: A.First", details)
        self.assertIn("  propriedade reprovada: B.Second", details)

    def test_a_broken_property_of_the_service_is_named_with_its_class_and_message(self) -> None:
        service = (
            '<testsuites><testsuite name="pytest">'
            '<testcase classname="tests.properties.test_secret" name="t">'
            '<failure message="AssertionError: falsifying example" />'
            "</testcase></testsuite></testsuites>"
        )

        details = self.details(trx((PROPERTY_CLASS, "Passed")), service)

        self.assertIn("  propriedade reprovada: tests.properties.test_secret.t", details)
        self.assertIn("    mensagem: AssertionError: falsifying example", details)
