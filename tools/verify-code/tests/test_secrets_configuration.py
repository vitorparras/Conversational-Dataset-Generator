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
TOP_LEVEL_KEYS = {"title", "extend", "allowlists"}
NEXT_JOB = re.compile(r"\n  [a-z_-]+:\n")


def ignore_entries(text: str) -> list[str]:
    """The lines of a .gitleaksignore that are not comments or blank."""
    return [line for line in text.splitlines() if line.strip() and not line.startswith("#")]


def malformed(entries: list[str]) -> list[str]:
    """The entries that are not `<40 hex commit>:<path>:<rule>:<line>`."""
    return [entry for entry in entries if not FINGERPRINT.match(entry)]


def job_of(text: str, name: str) -> str:
    """The text of one job of the workflow: from its key to the next job."""
    after = text.split(f"\n  {name}:", 1)[1]
    return NEXT_JOB.split(after, maxsplit=1)[0]


class RepositoryConfigurationTests(unittest.TestCase):
    def setUp(self) -> None:
        self.config = tomllib.loads(REPO_CONFIG)

    def test_the_default_rules_stay_on_and_nothing_else_is_extended(self) -> None:
        self.assertEqual(set(self.config["extend"]), {"useDefault"})
        self.assertIs(self.config["extend"]["useDefault"], True)

    def test_the_top_level_has_no_key_that_could_disable_or_override_a_rule(self) -> None:
        self.assertEqual(set(self.config), TOP_LEVEL_KEYS)

    def test_the_only_allowance_is_the_synthetic_marker(self) -> None:
        allowlists = self.config["allowlists"]

        self.assertEqual(len(allowlists), 1)
        self.assertEqual(allowlists[0]["regexes"], ["^ZQX-[A-Za-z0-9-]+$"])

    def test_the_allowance_has_no_scope_option_that_could_widen_or_narrow_it(self) -> None:
        self.assertEqual(set(self.config["allowlists"][0]), ALLOWANCE_KEYS)


class IgnoreFileTests(unittest.TestCase):
    def setUp(self) -> None:
        self.entries = ignore_entries(IGNORE_FILE.read_text(encoding="utf-8"))

    def test_every_entry_is_a_full_fingerprint_with_its_commit(self) -> None:
        self.assertEqual(malformed(self.entries), [])

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


class JobOfTests(unittest.TestCase):
    def test_a_job_ends_where_the_next_job_starts(self) -> None:
        text = "jobs:\n  a:\n    x: 1\n  b:\n    y: 2\n  c:\n    z: 3\n"

        self.assertEqual(job_of(text, "b"), "\n    y: 2")

    def test_a_job_name_with_a_hyphen_ends_the_previous_job(self) -> None:
        text = "jobs:\n  a:\n    x: 1\n  b-c:\n    y: 2\n"

        self.assertEqual(job_of(text, "a"), "\n    x: 1")
