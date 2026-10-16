"""Tests for the trivy step: the command, the JSON report, accepted risks and tool failures."""

from __future__ import annotations

import itertools
import json
import tempfile
import unittest
from datetime import date
from pathlib import Path
from typing import Any

from steps import accepted_risks, trivy, trivy_binary
from steps.common import CommandOutcome, StepResult

from . import support

ROOT = support.VERIFY_CODE.parents[1]
WORKFLOW = ROOT / ".github" / "workflows" / "verify-code.yml"
ACTION = ROOT / ".github" / "actions" / "install-trivy" / "action.yml"
TODAY = date(2026, 10, 16)
LOCK = "core/src/Zqx/packages.lock.json"
VULNERABILITY: dict[str, Any] = {
    "Target": LOCK,
    "Vulnerabilities": [
        {
            "VulnerabilityID": "CVE-0000-0001",
            "PkgName": "Zqx.Package",
            "Severity": "HIGH",
            "Status": "fixed",
        }
    ],
}
SECRET: dict[str, Any] = {
    "Target": "src/a.py",
    "Secrets": [{"RuleID": "github-pat", "Severity": "CRITICAL", "Match": "token = ****"}],
}
MISCONFIGURATION: dict[str, Any] = {
    "Target": "Dockerfile",
    "Misconfigurations": [
        {"ID": "DS-0002", "Severity": "HIGH", "Status": "FAIL"},
        {"ID": "DS-0099", "Severity": "HIGH", "Status": "PASS"},
    ],
}


def report(*results: dict[str, Any]) -> dict[str, str]:
    return {trivy.REPORT_FILE: json.dumps({"SchemaVersion": 2, "Results": list(results)})}


def register(expires: str, subject: str = "Zqx.Package") -> str:
    lines = [
        "[[risk]]",
        'id = "CVE-0000-0001"',
        f'subject = "{subject}"',
        f"expires = {expires}",
        'reason = "sem versão corrigida"',
    ]
    return "\n".join(lines) + "\n"


def lines_with(details: tuple[str, ...], text: str) -> list[str]:
    """The report lines that contain ``text``."""
    return [line for line in details if text in line]


def job_of(text: str, name: str) -> str:
    """The text of one job of the workflow: from its key to the next job."""
    return text.split(f"\n  {name}:", 1)[1].split("\n  pr_size:", 1)[0]


def skipped_folders(command: list[str]) -> list[str]:
    """The value after every `--skip-dirs` of a command."""
    return [value for flag, value in itertools.pairwise(command) if flag == "--skip-dirs"]


class TrivyStepTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)
        support.write(self.root, accepted_risks.FILE, "# nenhum risco aceito\n")
        self.folder = self.root / support.OUTPUT_DIRECTORY / trivy.REPORT_DIRECTORY

    def run_fake(
        self, files: dict[str, str], outcome: CommandOutcome = support.SUCCESS
    ) -> tuple[StepResult, support.FakeRunner]:
        runner = support.FakeRunner(outcome, files, self.folder)
        return trivy.scan(support.context(self.root, runner), "trivy", TODAY), runner

    def test_the_command_scans_the_three_kinds_at_high_and_critical(self) -> None:
        _, runner = self.run_fake(report())

        command = runner.commands[0]
        self.assertEqual(command[1:3], ["fs", "--scanners"])
        self.assertIn("vuln,secret,misconfig", command)
        self.assertIn("HIGH,CRITICAL", command)
        self.assertEqual(command[-1], ".")

    def test_the_step_decides_the_verdict_so_the_tool_exits_zero_on_findings(self) -> None:
        _, runner = self.run_fake(report())

        command = runner.commands[0]
        self.assertEqual(command[command.index("--exit-code") + 1], "0")

    def test_the_environment_and_report_folders_are_skipped(self) -> None:
        _, runner = self.run_fake(report())

        self.assertEqual(
            skipped_folders(runner.commands[0]),
            [".verify-code", "**/.venv", "**/bin", "**/obj"],
        )

    def test_a_high_vulnerability_fails_naming_id_package_and_lock(self) -> None:
        result, _ = self.run_fake(report(VULNERABILITY))

        self.assertFalse(result.ok)
        self.assertIn(
            f"  - HIGH  vulnerabilidade CVE-0000-0001  Zqx.Package  ({LOCK})", result.details
        )

    def test_a_secret_fails_by_rule_and_file_and_never_by_the_matched_text(self) -> None:
        result, _ = self.run_fake(report(SECRET))

        self.assertFalse(result.ok)
        self.assertIn("  - CRITICAL  segredo github-pat  src/a.py  (src/a.py)", result.details)
        self.assertNotIn("token = ****", "\n".join(result.details))

    def test_only_failed_misconfiguration_checks_count(self) -> None:
        result, _ = self.run_fake(report(MISCONFIGURATION))

        self.assertEqual(
            lines_with(result.details, "DS-"),
            ["  - HIGH  configuração DS-0002  Dockerfile  (Dockerfile)"],
        )

    def test_an_empty_report_passes(self) -> None:
        result, _ = self.run_fake(report())

        self.assertTrue(result.ok, result.details)

    def test_an_accepted_risk_within_its_deadline_passes_and_is_counted(self) -> None:
        support.write(self.root, accepted_risks.FILE, register("2026-10-16"))

        result, _ = self.run_fake(report(VULNERABILITY))

        self.assertTrue(result.ok, result.details)
        self.assertIn("(1 com risco aceito)", result.details[0])

    def test_an_expired_risk_fails_naming_it_even_if_nothing_matches(self) -> None:
        support.write(self.root, accepted_risks.FILE, register("2026-10-15"))

        result, runner = self.run_fake(report())

        self.assertFalse(result.ok)
        self.assertIn(
            "risco aceito venceu em 2026-10-15: CVE-0000-0001 em Zqx.Package", result.details
        )
        self.assertEqual(runner.commands, [])

    def test_a_risk_for_another_package_does_not_cover_the_finding(self) -> None:
        support.write(self.root, accepted_risks.FILE, register("2026-12-31", "Other.Package"))

        result, _ = self.run_fake(report(VULNERABILITY))

        self.assertFalse(result.ok)

    def test_a_missing_register_fails_before_running_the_tool(self) -> None:
        (self.root / accepted_risks.FILE).unlink()

        result, runner = self.run_fake(report())

        self.assertFalse(result.ok)
        self.assertEqual(runner.commands, [])

    def test_a_tool_error_fails_even_with_a_report_and_shows_the_tail(self) -> None:
        result, _ = self.run_fake(report(), CommandOutcome(False, "FATAL db download failed"))

        self.assertFalse(result.ok)
        self.assertIn("  | FATAL db download failed", result.details)

    def test_telemetry_and_the_version_check_are_off_so_nothing_is_reported_out(self) -> None:
        _, runner = self.run_fake(report())

        self.assertIn("--disable-telemetry", runner.commands[0])
        self.assertIn("--skip-version-check", runner.commands[0])

    def test_a_report_that_is_not_an_object_fails(self) -> None:
        result, _ = self.run_fake({trivy.REPORT_FILE: "[]"})

        self.assertFalse(result.ok)

    def test_a_report_that_is_not_json_fails_even_when_the_tool_exits_zero(self) -> None:
        result, _ = self.run_fake({trivy.REPORT_FILE: "not json"})

        self.assertFalse(result.ok)

    def test_a_run_that_leaves_no_report_fails(self) -> None:
        result, _ = self.run_fake({})

        self.assertFalse(result.ok)


class TrivyPinTests(unittest.TestCase):
    def test_a_folder_without_the_binary_fails_naming_the_installation(self) -> None:
        with tempfile.TemporaryDirectory() as folder:
            located = trivy_binary.locate(folder)

        self.assertIsNone(located.path)
        self.assertIn(trivy_binary.VERSION, located.problem)
        self.assertIn(trivy_binary.RELEASE_URL, located.problem)

    def test_the_install_action_downloads_the_pinned_linux_archive(self) -> None:
        text = ACTION.read_text(encoding="utf-8")

        self.assertIn(f"{trivy_binary.RELEASE_URL}/{trivy_binary.PINS['linux'].archive}", text)


class WorkflowTests(unittest.TestCase):
    def setUp(self) -> None:
        self.text = WORKFLOW.read_text(encoding="utf-8")
        self.job = job_of(self.text, "trivy")

    def test_the_job_installs_the_pinned_trivy_and_runs_the_step(self) -> None:
        self.assertIn("uses: ./.github/actions/install-trivy", self.job)
        self.assertIn("verify.py --step trivy", self.job)

    def test_the_job_runs_on_every_event_including_the_weekly_schedule(self) -> None:
        self.assertNotIn("if:", self.job)
        self.assertIn("schedule:\n    - cron:", self.text)

    def test_the_vulnerability_database_is_cached_and_restored_from_an_older_run(self) -> None:
        self.assertIn("actions/cache@", self.job)
        self.assertIn("path: ~/.cache/trivy", self.job)
        self.assertIn("restore-keys: trivy-db-", self.job)
