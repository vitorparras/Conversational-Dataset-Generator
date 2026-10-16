"""Tests for the secrets step: a real gitleaks over synthetic repositories, and fake runs."""

from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

from steps import secrets
from steps.common import CommandOutcome, StepResult

from . import support

ROOT = support.VERIFY_CODE.parents[1]
REPO_CONFIG = (ROOT / secrets.CONFIG).read_text(encoding="utf-8")
PLAIN_CONFIG = "[extend]\nuseDefault = true\n"
TOKEN = "ghp_" + "aB3dE5gH7jK9mN1pQ3sT5vW7yZ9bC1dE3fG5"
TOKEN_LINE = f'token = "{TOKEN}"\n'
MARKER_LINE = 'api_key = "ZQX-MARKER-7731-default"\n'
FINDING: dict[str, object] = {
    "RuleID": "github-pat",
    "File": "src/a.py",
    "StartLine": 3,
    "Commit": "abcdef0123456789",
    "Secret": "REDACTED",
}


def commit(repo: Path, name: str, content: str) -> None:
    support.write(repo, name, content)
    support.git(repo, "add", ".")
    support.git(repo, "commit", "-q", "-m", name)


def log_limits(command: list[str]) -> list[str]:
    """The `--log-opts` arguments of a command."""
    return [argument for argument in command if argument.startswith("--log-opts")]


def fake_report(findings: list[dict[str, object]]) -> dict[str, str]:
    return {secrets.REPORT_FILE: json.dumps(findings)}


class ScanRepositoryTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.repo = support.git_repository(Path(self._tmp.name))
        commit(self.repo, secrets.CONFIG, REPO_CONFIG)

    def scan(self, base: str | None) -> StepResult:
        return secrets.run(support.context(self.repo, base=base))

    def test_a_token_in_the_diff_fails_naming_rule_file_and_line(self) -> None:
        commit(self.repo, "src/a.py", "x = 1\n" + TOKEN_LINE)

        result = self.scan("main")

        self.assertFalse(result.ok)
        self.assertIn("  - github-pat  src/a.py:2  (commit", " ".join(result.details))

    def test_the_secret_value_never_reaches_the_report(self) -> None:
        commit(self.repo, "src/a.py", TOKEN_LINE)

        result = self.scan("main")

        self.assertNotIn(TOKEN, "\n".join(result.details))
        self.assertNotIn(TOKEN[:12], "\n".join(result.details))

    def test_a_clean_diff_passes(self) -> None:
        commit(self.repo, "src/a.py", "x = 1\n")

        result = self.scan("main")

        self.assertTrue(result.ok, result.details)

    def test_the_synthetic_marker_is_allowed(self) -> None:
        commit(self.repo, "src/a.py", MARKER_LINE)

        result = self.scan("main")

        self.assertTrue(result.ok, result.details)

    def test_the_same_marker_fails_without_the_allowance(self) -> None:
        commit(self.repo, secrets.CONFIG, PLAIN_CONFIG)
        commit(self.repo, "src/a.py", MARKER_LINE)

        result = self.scan("main")

        self.assertFalse(result.ok)

    def test_a_token_removed_in_a_later_commit_is_still_found_in_the_history(self) -> None:
        commit(self.repo, "src/a.py", TOKEN_LINE)
        commit(self.repo, "src/a.py", "x = 1\n")

        result = self.scan(None)

        self.assertFalse(result.ok)
        self.assertIn("o histórico inteiro", result.details[0])

    def test_commits_before_the_base_are_not_scanned_in_a_pull_request(self) -> None:
        commit(self.repo, "src/a.py", TOKEN_LINE)
        support.git(self.repo, "branch", "-f", "main", "HEAD")
        commit(self.repo, "src/b.py", "y = 2\n")

        result = self.scan("main")

        self.assertTrue(result.ok, result.details)


class ScanWithFakeRunnerTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)
        support.write(self.root, secrets.CONFIG, REPO_CONFIG)
        self.folder = self.root / support.OUTPUT_DIRECTORY / secrets.REPORT_DIRECTORY

    def run_fake(
        self, outcome: CommandOutcome, files: dict[str, str], base: str | None = None
    ) -> tuple[StepResult, support.FakeRunner]:
        runner = support.FakeRunner(outcome, files, self.folder)
        return secrets.scan(support.context(self.root, runner, base), "gitleaks"), runner

    def test_the_command_always_redacts_and_limits_the_log_to_the_base(self) -> None:
        _, runner = self.run_fake(support.SUCCESS, fake_report([]), "origin/main")

        self.assertIn("--redact", runner.commands[0])
        self.assertIn("--log-opts=origin/main..HEAD", runner.commands[0])

    def test_without_a_base_the_command_has_no_log_limit(self) -> None:
        _, runner = self.run_fake(support.SUCCESS, fake_report([]))

        self.assertEqual(log_limits(runner.commands[0]), [])

    def test_findings_are_reported_without_the_tool_output(self) -> None:
        outcome = CommandOutcome(False, "Author: ZQX-AUTHOR-0001")

        result, _ = self.run_fake(outcome, fake_report([FINDING]))

        self.assertFalse(result.ok)
        self.assertIn("  - github-pat  src/a.py:3  (commit abcdef0)", result.details)
        self.assertNotIn("ZQX-AUTHOR-0001", "\n".join(result.details))

    def test_a_failed_run_with_an_empty_report_is_a_tool_error_and_not_a_pass(self) -> None:
        outcome = CommandOutcome(False, "fatal: bad revision")

        result, _ = self.run_fake(outcome, fake_report([]))

        self.assertFalse(result.ok)
        self.assertIn("  | fatal: bad revision", result.details)

    def test_the_tool_never_runs_verbose(self) -> None:
        _, runner = self.run_fake(support.SUCCESS, fake_report([]))

        self.assertNotIn("-v", runner.commands[0])
        self.assertNotIn("--verbose", runner.commands[0])

    def test_a_report_that_is_not_a_list_fails(self) -> None:
        result, _ = self.run_fake(support.SUCCESS, {secrets.REPORT_FILE: '{"leaks": []}'})

        self.assertFalse(result.ok)

    def test_a_report_that_is_not_json_fails_even_when_the_tool_exits_zero(self) -> None:
        result, _ = self.run_fake(support.SUCCESS, {secrets.REPORT_FILE: "not json"})

        self.assertFalse(result.ok)

    def test_the_report_is_deleted_after_it_is_read(self) -> None:
        self.run_fake(support.SUCCESS, fake_report([FINDING]))

        self.assertFalse(self.folder.exists())

    def test_a_run_that_leaves_no_report_fails(self) -> None:
        result, _ = self.run_fake(support.SUCCESS, {})

        self.assertFalse(result.ok)

    def test_a_missing_config_fails_naming_the_file(self) -> None:
        (self.root / secrets.CONFIG).unlink()

        result, _ = self.run_fake(support.SUCCESS, fake_report([]))

        self.assertFalse(result.ok)
        self.assertIn(f"{secrets.CONFIG} não existe", result.details[0])
