"""Tests for the core half of the dependencies step: the audited restore and NuGet suppressions."""

from __future__ import annotations

import tempfile
import unittest
from datetime import date
from pathlib import Path

from steps import core_audit
from steps.accepted_risk import Risk
from steps.common import CommandOutcome, StepResult

from . import dependency_fixtures as fx
from . import support

DEADLINE = date(2026, 12, 31)
ACCEPTED = Risk(fx.ADVISORY, "Zqx.Leaf", DEADLINE, "sem versão corrigida")
WRONG_PACKAGE = Risk(fx.ADVISORY, "Zqx.Absent", DEADLINE, "sem versão corrigida")
PART_OF_THE_URL = Risk("GHSA-zqx0-0000-0001", "Zqx.Leaf", DEADLINE, "sem versão corrigida")
CHAIN = "Zqx.App > Zqx.Direct > Zqx.Middle > Zqx.Leaf"
OFFLINE = "error NU1900: Warning As Error: Error occurred while getting package vulnerability data"
RESTORE_COMMAND = [
    *("dotnet", "restore", "core/Core.slnx", "--locked-mode", "--force"),
    *("-p:NuGetAudit=true", "-p:NuGetAuditMode=all", "-p:NuGetAuditLevel=low"),
    *("-p:NoWarn=", "-p:TreatWarningsAsErrors=false"),
    "-p:WarningsAsErrors=NU1900%3BNU1901%3BNU1902%3BNU1903%3BNU1904%3BNU1905",
]
SILENCED = (
    "<Project><ItemGroup>"
    '<PackageReference Include="Zqx.Direct" NoWarn="NU1903" />'
    "</ItemGroup></Project>\n"
)


class CoreAuditTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = fx.locked_repository(Path(self._tmp.name))

    def audit(
        self, outcome: CommandOutcome = support.SUCCESS, risks: tuple[Risk, ...] = ()
    ) -> tuple[StepResult, support.FakeRunner]:
        runner = support.FakeRunner(outcome)
        return core_audit.audit(support.context(self.root, runner), risks), runner

    def suppress(self) -> None:
        support.write(self.root, "core/Directory.Packages.props", fx.suppression())

    def test_the_restore_is_locked_forced_audited_at_any_severity_with_audit_errors(self) -> None:
        self.assertEqual(core_audit.restore_command(), RESTORE_COMMAND)

    def test_a_transitive_vulnerability_fails_naming_package_advisory_and_path(self) -> None:
        result, _ = self.audit(CommandOutcome(False, fx.restore_error(self.root)))

        self.assertFalse(result.ok)
        self.assertEqual(result.details[1], f"  - Zqx.Leaf 1.0.0: {fx.ADVISORY}  (via {CHAIN})")

    def test_a_low_severity_vulnerability_is_named_too(self) -> None:
        output = fx.restore_error(self.root, "NU1901", "low")

        result, _ = self.audit(CommandOutcome(False, output))

        self.assertEqual(result.details[1], f"  - Zqx.Leaf 1.0.0: {fx.ADVISORY}  (via {CHAIN})")

    def test_a_vulnerability_printed_twice_is_named_once(self) -> None:
        output = fx.restore_error(self.root) * 2

        result, _ = self.audit(CommandOutcome(False, output))

        self.assertEqual(result.details[0], "núcleo: 1 vulnerabilidade(s) sem risco aceito")

    def test_a_missing_lock_fails_before_the_restore_could_create_it(self) -> None:
        (self.root / fx.CORE_LOCK).unlink()

        result, runner = self.audit()

        self.assertFalse(result.ok)
        self.assertIn("core/src/Zqx.App/packages.lock.json não existe", result.details[0])
        self.assertEqual(runner.commands, [])

    def test_a_missing_solution_fails_without_running_anything(self) -> None:
        (self.root / "core/Core.slnx").unlink()

        result, runner = self.audit()

        self.assertFalse(result.ok)
        self.assertEqual(runner.commands, [])

    def test_a_solution_without_projects_fails(self) -> None:
        support.write(self.root, "core/Core.slnx", "<Solution />\n")

        result, _ = self.audit()

        self.assertFalse(result.ok)
        self.assertIn("não lista projeto", result.details[0])

    def test_an_unreachable_vulnerability_source_fails_with_the_tail(self) -> None:
        result, _ = self.audit(CommandOutcome(False, OFFLINE))

        self.assertFalse(result.ok)
        self.assertIn(f"  | {OFFLINE}", result.details)

    def test_a_clean_restore_passes(self) -> None:
        result, _ = self.audit()

        self.assertTrue(result.ok, result.details)

    def test_a_suppression_without_an_accepted_risk_fails(self) -> None:
        self.suppress()

        result, _ = self.audit()

        self.assertFalse(result.ok)
        self.assertIn(f"NuGetAuditSuppress {fx.ADVISORY} sem risco aceito", result.details[0])

    def test_a_suppression_with_an_accepted_risk_passes_and_lists_it(self) -> None:
        self.suppress()

        result, _ = self.audit(risks=(ACCEPTED,))

        self.assertTrue(result.ok, result.details)
        self.assertIn(
            f"  - risco aceito até 2026-12-31: {fx.ADVISORY} em Zqx.Leaf", result.details[1]
        )

    def test_a_risk_on_a_package_outside_the_locks_does_not_cover_a_suppression(self) -> None:
        self.suppress()

        result, _ = self.audit(risks=(WRONG_PACKAGE,))

        self.assertFalse(result.ok)

    def test_a_risk_whose_id_is_only_part_of_the_advisory_does_not_cover_it(self) -> None:
        self.suppress()

        result, _ = self.audit(risks=(PART_OF_THE_URL,))

        self.assertFalse(result.ok)

    def test_a_suppression_in_another_form_fails(self) -> None:
        text = "<Project><ItemGroup><NuGetAuditSuppress Include='x' /></ItemGroup></Project>\n"
        support.write(self.root, fx.PROJECT, text)

        result, _ = self.audit(risks=(ACCEPTED,))

        self.assertFalse(result.ok)
        self.assertIn("fora da forma", result.details[0])

    def test_an_audit_code_silenced_on_a_package_reference_fails(self) -> None:
        support.write(self.root, fx.PROJECT, SILENCED)

        result, _ = self.audit()

        self.assertFalse(result.ok)
        self.assertIn("código NU190x calado em core/", result.details[0])

    def test_a_suppression_inside_a_build_output_folder_is_not_read(self) -> None:
        support.write(self.root, "core/src/Zqx.App/obj/Zqx.App.props", fx.suppression())

        result, _ = self.audit()

        self.assertTrue(result.ok, result.details)
