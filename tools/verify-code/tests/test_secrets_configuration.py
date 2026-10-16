"""Tests for the files that configure the secrets step: rules, ignore list and workflow job."""

from __future__ import annotations

import re
import tomllib
import unittest

from steps import secrets

from . import support

ROOT = support.VERIFY_CODE.parents[1]
WORKFLOW = ROOT / ".github" / "workflows" / "verify-code.yml"
REPO_CONFIG = (ROOT / secrets.CONFIG).read_text(encoding="utf-8")
IGNORE_FILE = ROOT / ".gitleaksignore"
ACCEPTED_FINGERPRINT = (
    "e23639b4972b0191179533d5001c6935ed501cfd:language-service/tests/conftest.py:generic-api-key:16"
)
FINGERPRINT = re.compile(r"^[0-9a-f]{40}:[^:]+:[a-z0-9-]+:[0-9]+$")
ALLOWANCE_KEYS = {"description", "regexes"}


def ignore_entries(text: str) -> list[str]:
    """The lines of a .gitleaksignore that are not comments or blank."""
    return [line for line in text.splitlines() if line.strip() and not line.startswith("#")]


def job_of(text: str, name: str) -> str:
    """The text of one job of the workflow: from its key to the next job."""
    return text.split(f"\n  {name}:", 1)[1].split("\n  pr_size:", 1)[0]


class RepositoryConfigurationTests(unittest.TestCase):
    def setUp(self) -> None:
        self.config = tomllib.loads(REPO_CONFIG)

    def test_the_default_rules_stay_on(self) -> None:
        self.assertTrue(self.config["extend"]["useDefault"])

    def test_the_only_allowance_is_the_synthetic_marker(self) -> None:
        allowlists = self.config["allowlists"]

        self.assertEqual(len(allowlists), 1)
        self.assertEqual(allowlists[0]["regexes"], ["ZQX-[A-Za-z0-9-]+"])

    def test_the_allowance_has_no_scope_option_that_could_widen_or_narrow_it(self) -> None:
        self.assertEqual(set(self.config["allowlists"][0]), ALLOWANCE_KEYS)


class IgnoreFileTests(unittest.TestCase):
    def setUp(self) -> None:
        self.entries = ignore_entries(IGNORE_FILE.read_text(encoding="utf-8"))

    def test_every_entry_is_a_full_fingerprint_with_its_commit(self) -> None:
        self.assertEqual([e for e in self.entries if not FINGERPRINT.match(e)], [])

    def test_the_only_entry_is_the_accepted_one(self) -> None:
        self.assertEqual(self.entries, [ACCEPTED_FINGERPRINT])


class WorkflowTests(unittest.TestCase):
    def setUp(self) -> None:
        self.text = WORKFLOW.read_text(encoding="utf-8")
        self.job = job_of(self.text, "secrets")

    def test_the_history_is_fetched_whole(self) -> None:
        self.assertIn("fetch-depth: 0", self.job)

    def test_the_job_runs_on_every_event_including_the_weekly_schedule(self) -> None:
        self.assertNotIn("if:", self.job)
        self.assertIn("schedule:\n    - cron:", self.text)

    def test_the_job_installs_gitleaks_before_running_the_step(self) -> None:
        self.assertLess(
            self.job.index("uses: ./.github/actions/install-gitleaks"),
            self.job.index("verify.py --step secrets"),
        )

    def test_the_step_runs_against_the_base_of_the_pull_request(self) -> None:
        self.assertIn("format('--base origin/{0}', github.base_ref)", self.job)
