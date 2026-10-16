"""Tests for the Dependabot setup: ecosystems, queue limit, grouping and pinned actions."""

from __future__ import annotations

import re
import unittest

from . import support

GITHUB = support.VERIFY_CODE.parents[1] / ".github"
CONFIG = GITHUB / "dependabot.yml"
LOCKFILES = GITHUB / "workflows" / "dependabot-lockfiles.yml"
QUEUE_LIMIT = 3
FULL_SHA = re.compile(r"^[0-9a-f]{40} # v\d+(\.\d+)*$")


def update_blocks(text: str) -> dict[str, str]:
    """The text of each `updates` entry, keyed by its package ecosystem."""
    entries = re.split(r"(?m)^  - package-ecosystem: ", text)[1:]
    return {entry.split()[0]: entry for entry in entries}


def field(block: str, key: str) -> str:
    """The value of the first `key:` line of ``block``."""
    match = re.search(rf"(?m)^\s+{key}: (.+)$", block)
    assert match is not None, key
    return match.group(1).strip("\"'")


def action_references() -> list[str]:
    """Everything after `uses:` in every workflow of the repository."""
    lines = [
        line
        for path in GITHUB.glob("workflows/*.yml")
        for line in path.read_text("utf-8").splitlines()
    ]
    return [line.split("uses:", 1)[1].strip().split("@", 1)[1] for line in lines if "uses:" in line]


class DependabotConfigTests(unittest.TestCase):
    def setUp(self) -> None:
        self.blocks = update_blocks(CONFIG.read_text(encoding="utf-8"))

    def test_the_three_ecosystems_are_covered(self) -> None:
        self.assertEqual(set(self.blocks), {"nuget", "uv", "github-actions"})

    def test_each_ecosystem_reads_its_own_directory(self) -> None:
        directories = {name: field(block, "directory") for name, block in self.blocks.items()}

        self.assertEqual(
            directories, {"nuget": "/core", "uv": "/language-service", "github-actions": "/"}
        )

    def test_every_ecosystem_updates_weekly(self) -> None:
        intervals = {field(block, "interval") for block in self.blocks.values()}

        self.assertEqual(intervals, {"weekly"})

    def test_the_open_pull_request_queue_stays_small(self) -> None:
        limits = [int(field(block, "open-pull-requests-limit")) for block in self.blocks.values()]

        self.assertEqual(len(limits), 3)
        self.assertLessEqual(max(limits), QUEUE_LIMIT)

    def test_every_ecosystem_groups_all_packages_into_one_pull_request(self) -> None:
        ungrouped = [
            name
            for name, block in self.blocks.items()
            if f'      {name}:\n        patterns: ["*"]' not in block
        ]

        self.assertEqual(ungrouped, [])

    def test_commit_messages_are_conventional(self) -> None:
        prefixes = {name: field(block, "prefix") for name, block in self.blocks.items()}

        self.assertEqual(
            prefixes, {"nuget": "build(deps)", "uv": "build(deps)", "github-actions": "ci(deps)"}
        )


class PinnedActionsTests(unittest.TestCase):
    def test_every_action_is_pinned_by_sha_with_a_version_comment(self) -> None:
        references = action_references()

        self.assertGreater(len(references), 0)
        self.assertEqual([r for r in references if not FULL_SHA.match(r)], [])


class LockfileWorkflowTests(unittest.TestCase):
    def setUp(self) -> None:
        self.text = LOCKFILES.read_text(encoding="utf-8")

    def test_it_runs_only_for_the_dependabot_nuget_branches(self) -> None:
        self.assertIn("github.actor == 'dependabot[bot]'", self.text)
        self.assertIn("startsWith(github.head_ref, 'dependabot/nuget/')", self.text)

    def test_write_permission_is_declared_on_the_job_and_not_on_the_workflow(self) -> None:
        top, job = self.text.split("jobs:", 1)

        self.assertNotIn("contents: write", top)
        self.assertIn("contents: write", job)

    def test_it_rewrites_the_locks_by_forcing_the_evaluation(self) -> None:
        self.assertIn("dotnet restore core/Core.slnx --force-evaluate", self.text)

    def test_it_pushes_to_the_branch_of_the_pull_request_and_not_to_main(self) -> None:
        self.assertIn("ref: ${{ github.head_ref }}", self.text)
        self.assertNotIn("push origin", self.text)
