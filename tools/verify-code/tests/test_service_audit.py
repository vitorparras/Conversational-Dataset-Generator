"""Tests for the service half of the dependencies step: `uv export` and pip-audit's report."""

from __future__ import annotations

import sys
import tempfile
import unittest
from datetime import date
from pathlib import Path

from steps import service_audit
from steps.accepted_risk import Risk
from steps.common import CommandOutcome, StepResult

from . import dependency_fixtures as fx
from . import support

DEADLINE = date(2026, 12, 31)
FIRST = Risk("CVE-0000-0002", "zqx-leaf", DEADLINE, "sem versão corrigida")
SECOND = Risk("CVE-0000-0004", "zqx-leaf", DEADLINE, "sem versão corrigida")
OTHER_PACKAGE = Risk("CVE-0000-0002", "zqx-web", DEADLINE, "sem versão corrigida")
CHAIN = "language-service > zqx-web > zqx-leaf"
NO_MODULE = "No module named pip_audit"
EXPORT_COMMAND = [
    *("uv", "export", "--locked", "--all-groups", "--all-extras", "--no-emit-project"),
    *("--format", "requirements-txt", "--directory", "language-service"),
    *("--output-file", "x.txt", "--quiet"),
]
AUDIT_COMMAND = [
    *(sys.executable, "-m", "pip_audit", "--requirement", "r.txt", "--disable-pip"),
    *("--require-hashes", "--strict", "--progress-spinner", "off", "--format", "json"),
    *("--output", "a.json"),
]


class UrlRequirementTests(unittest.TestCase):
    def test_requirement_by_url_leaves_the_export_with_its_hashes(self) -> None:
        keep = ["alpha==1.0 \\", "    --hash=sha256:aa", "omega==2.0 \\", "    --hash=sha256:cc"]
        url = ["model @ https://example.invalid/model.whl \\", "    --hash=sha256:bb"]
        export = "\n".join([*keep[:2], *url, *keep[2:]]) + "\n"

        kept = service_audit.without_url_requirements(export)

        self.assertEqual("\n".join(keep) + "\n", kept)


class ServiceAuditTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = fx.locked_repository(Path(self._tmp.name))

    def audit(
        self,
        report: str | None,
        outcome: CommandOutcome = support.SUCCESS,
        risks: tuple[Risk, ...] = (),
    ) -> tuple[StepResult, fx.ServiceRunner]:
        runner = fx.ServiceRunner(fx.report_folder(self.root), report, outcome)
        return service_audit.audit(support.context(self.root, runner), risks), runner

    def test_the_export_takes_the_whole_locked_tree_with_hashes_without_the_project(self) -> None:
        command = service_audit.export_command(Path("x.txt"))

        self.assertEqual(command, EXPORT_COMMAND)

    def test_the_audit_reads_hashes_without_pip_and_ignores_nothing(self) -> None:
        command = service_audit.audit_command(Path("r.txt"), Path("a.json"))

        self.assertEqual(command, AUDIT_COMMAND)

    def test_a_package_locked_for_another_platform_reaches_the_audit_without_marker(self) -> None:
        _, runner = self.audit(fx.audit_report())

        self.assertEqual(
            runner.audited,
            "zqx-web==2.0 \\\n    --hash=sha256:00\nzqx-leaf==1.0\\\n    --hash=sha256:01\n",
        )

    def test_a_transitive_vulnerability_fails_naming_package_ids_and_path(self) -> None:
        result, _ = self.audit(fx.audit_report(fx.PYTHON_VULNERABLE))

        self.assertFalse(result.ok)
        self.assertEqual(
            result.details[1], f"  - zqx-leaf 1.0: PYSEC-0000-1, CVE-0000-0002  (via {CHAIN})"
        )

    def test_every_vulnerability_of_a_package_is_judged_not_only_the_first(self) -> None:
        result, _ = self.audit(fx.audit_report(fx.PYTHON_VULNERABLE), risks=(FIRST,))

        self.assertFalse(result.ok)
        self.assertEqual(
            result.details,
            (
                "serviço: 1 vulnerabilidade(s) sem risco aceito",
                f"  - zqx-leaf 1.0: PYSEC-0000-3, CVE-0000-0004  (via {CHAIN})",
            ),
        )

    def test_risks_accepted_by_alias_within_their_deadline_pass_and_are_listed(self) -> None:
        result, _ = self.audit(fx.audit_report(fx.PYTHON_VULNERABLE), risks=(FIRST, SECOND))

        self.assertTrue(result.ok, result.details)
        self.assertIn(
            "  - risco aceito até 2026-12-31: CVE-0000-0002 em zqx-leaf", result.details[1]
        )
        self.assertIn(
            "  - risco aceito até 2026-12-31: CVE-0000-0004 em zqx-leaf", result.details[2]
        )

    def test_a_risk_for_another_package_does_not_cover_the_finding(self) -> None:
        result, _ = self.audit(fx.audit_report(fx.PYTHON_VULNERABLE), risks=(OTHER_PACKAGE,))

        self.assertFalse(result.ok)

    def test_a_report_without_vulnerabilities_passes(self) -> None:
        result, _ = self.audit(fx.audit_report())

        self.assertTrue(result.ok, result.details)

    def test_a_missing_lock_fails_before_running_anything(self) -> None:
        (self.root / service_audit.LOCK).unlink()

        result, runner = self.audit(fx.audit_report())

        self.assertFalse(result.ok)
        self.assertIn("uv.lock não existe", result.details[0])
        self.assertEqual(runner.commands, [])

    def test_a_failed_export_fails_without_running_the_audit(self) -> None:
        outcome = CommandOutcome(False, "error: lockfile needs to be updated")

        result, runner = self.audit(fx.audit_report(), outcome)

        self.assertFalse(result.ok)
        self.assertIn("`uv export` falhou", result.details[0])
        self.assertEqual(len(runner.commands), 1)

    def test_a_missing_tool_fails_naming_how_to_install_it(self) -> None:
        result, _ = self.audit(None, CommandOutcome(True, NO_MODULE))

        self.assertFalse(result.ok)
        self.assertIn(f"pip install -r {service_audit.REQUIREMENTS}", result.details[1])
        self.assertIn(f"  | {NO_MODULE}", result.details)

    def test_a_report_that_audited_no_package_fails(self) -> None:
        result, _ = self.audit('{"dependencies": []}')

        self.assertFalse(result.ok)
        self.assertIn("não auditou nenhum pacote", result.details[0])

    def test_a_report_that_is_not_an_object_fails(self) -> None:
        result, _ = self.audit("[]")

        self.assertFalse(result.ok)

    def test_a_report_that_is_not_json_fails(self) -> None:
        result, _ = self.audit("not json")

        self.assertFalse(result.ok)
