"""Tests for the push to the main: it skips the heavy jobs only for a tree a green PR run tested.

The git commands run for real on a synthetic repository; the answers of the GitHub API are fakes.
"""

from __future__ import annotations

import contextlib
import io
import json
import tempfile
import unittest
from collections.abc import Sequence
from pathlib import Path
from typing import Any
from unittest import mock

from steps import tested_tree
from steps.common import CommandOutcome, Context, run_process

from . import support

verify = support.load_verify()
REPOSITORY = "dono/repo"
RUN = 4242
DOTNET = "Núcleo (.NET) e ferramentas"
PYTHON = "Serviço de linguagem (Python)"


class Github:
    """Answers `gh api` from a table of path prefixes and runs everything else for real."""

    def __init__(self, repo: Path, head: str, squash: str) -> None:
        self.repo, self.head, self.squash = repo, head, squash
        self.pulls: list[dict[str, Any]] = [
            {"number": 7, "merge_commit_sha": squash, "merged_at": "x", "head": {"sha": head}}
        ]
        self.runs: list[dict[str, Any]] = [
            {"id": RUN, "status": "completed", "conclusion": "success"}
        ]
        self.jobs = [
            {"name": DOTNET, "conclusion": "success"},
            {"name": PYTHON, "conclusion": "success"},
        ]
        self.names = [f"cobertura-pr-dotnet-{head}", f"cobertura-pr-python-{head}"]
        self.expired = False
        self.broken: set[str] = set()
        self.fetch_ok = True

    def __call__(self, command: Sequence[str], cwd: Path) -> CommandOutcome:
        if command[:2] == ["git", "fetch"]:
            return CommandOutcome(self.fetch_ok, "", 0 if self.fetch_ok else 128)
        if command[0] != "gh":
            return run_process(command, cwd)
        path = command[2]
        if any(part in path for part in self.broken):
            return CommandOutcome(False, "HTTP 502", 1)
        artifacts = [{"name": n, "expired": self.expired} for n in self.names]
        answers = {
            "/commits/": self.pulls,
            "/workflows/": {"workflow_runs": self.runs},
            "/jobs": {"jobs": self.jobs},
            "/artifacts": {"artifacts": artifacts},
        }
        return self.ok(next(body for part, body in answers.items() if part in path))

    @staticmethod
    def ok(body: object) -> CommandOutcome:
        return CommandOutcome(True, json.dumps(body), 0)


class TestedTreeTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.repo = support.git_repository(Path(self._tmp.name))
        support.write(self.repo, "docs/a.md", "novo\n")
        support.git(self.repo, "add", ".")
        support.git(self.repo, "commit", "-q", "-m", "head")
        self.head = support.git_output(self.repo, "rev-parse", "HEAD")
        support.git(self.repo, "checkout", "-q", "main")
        support.git(self.repo, "merge", "-q", "--squash", self.head)
        support.git(self.repo, "commit", "-q", "-m", "squash (#7)")
        self.squash = support.git_output(self.repo, "rev-parse", "HEAD")
        self.github = Github(self.repo, self.head, self.squash)

    def decide(self) -> tested_tree.Tested | None:
        ctx = Context(self.repo, self.repo / ".o", self.github)
        return tested_tree.tested(ctx, REPOSITORY, self.squash)

    def test_a_tree_a_green_run_tested_skips_and_promotes_the_tables_that_run_made(self) -> None:
        found = self.decide()

        self.assertEqual(found, tested_tree.Tested(RUN, self.head, ("dotnet", "python")))

    def test_a_run_that_skipped_the_heavy_jobs_skips_and_promotes_nothing(self) -> None:
        self.github.jobs = [{"name": DOTNET, "conclusion": "skipped"}]
        self.github.names = []

        self.assertEqual(self.decide(), tested_tree.Tested(RUN, self.head, ()))

    def test_a_table_the_run_should_have_kept_but_did_not_runs_everything(self) -> None:
        self.github.names = [f"cobertura-pr-dotnet-{self.head}"]

        self.assertIsNone(self.decide())

    def test_an_expired_table_runs_everything(self) -> None:
        self.github.expired = True

        self.assertIsNone(self.decide())

    def test_a_run_that_is_not_green_runs_everything(self) -> None:
        for conclusion in ("failure", "cancelled", None):
            with self.subTest(conclusion=conclusion):
                self.github.runs = [{"id": RUN, "status": "completed", "conclusion": conclusion}]

                self.assertIsNone(self.decide())

    def test_the_newest_finished_run_decides_not_an_older_green_one(self) -> None:
        self.github.runs = [
            {"id": 9, "status": "completed", "conclusion": "failure"},
            {"id": RUN, "status": "completed", "conclusion": "success"},
        ]

        self.assertIsNone(self.decide())

    def test_a_run_still_going_is_not_a_tested_tree(self) -> None:
        self.github.runs = [{"id": RUN, "status": "in_progress", "conclusion": None}]

        self.assertIsNone(self.decide())

    def test_a_commit_with_no_merged_pull_request_runs_everything(self) -> None:
        self.github.pulls = []

        self.assertIsNone(self.decide())

    def test_a_pull_request_that_merged_another_commit_runs_everything(self) -> None:
        self.github.pulls[0]["merge_commit_sha"] = "0" * 40

        self.assertIsNone(self.decide())

    def test_two_pull_requests_for_one_commit_run_everything(self) -> None:
        self.github.pulls.append(dict(self.github.pulls[0], number=8))

        self.assertIsNone(self.decide())

    def test_a_tree_that_differs_from_the_head_runs_everything(self) -> None:
        support.write(self.repo, "core/extra.cs", "x\n")
        support.git(self.repo, "add", ".")
        support.git(self.repo, "commit", "-q", "--amend", "-m", "squash (#7)")
        self.squash = support.git_output(self.repo, "rev-parse", "HEAD")
        self.github.squash = self.squash
        self.github.pulls[0]["merge_commit_sha"] = self.squash

        self.assertIsNone(self.decide())

    def test_a_head_that_does_not_contain_the_parent_runs_everything(self) -> None:
        support.git(self.repo, "reset", "-q", "--hard", "HEAD~1")
        support.git(self.repo, "commit", "-q", "--allow-empty", "-m", "outro merge")
        support.git(self.repo, "merge", "-q", "--squash", self.head)
        support.git(self.repo, "commit", "-q", "-m", "squash (#7)")
        self.squash = support.git_output(self.repo, "rev-parse", "HEAD")
        self.github.pulls[0]["merge_commit_sha"] = self.squash

        self.assertIsNone(self.decide())

    def test_a_merge_commit_with_two_parents_runs_everything(self) -> None:
        support.git(self.repo, "merge", "-q", "--no-ff", "-m", "merge", self.head)
        self.squash = support.git_output(self.repo, "rev-parse", "HEAD")
        self.github.pulls[0]["merge_commit_sha"] = self.squash

        self.assertIsNone(self.decide())

    def test_any_failure_to_ask_runs_everything(self) -> None:
        for part in ("/commits/", "/workflows/", "/jobs", "/artifacts"):
            with self.subTest(part=part):
                self.github.broken = {part}

                self.assertIsNone(self.decide())

    def test_a_head_that_cannot_be_fetched_runs_everything(self) -> None:
        self.github.fetch_ok = False

        self.assertIsNone(self.decide())


class CommandLineTests(unittest.TestCase):
    def main(self, found: tested_tree.Tested | None) -> tuple[str, str]:
        out, err = io.StringIO(), io.StringIO()
        with (
            mock.patch.object(verify.tested_tree, "tested", return_value=found),
            contextlib.redirect_stdout(out),
            contextlib.redirect_stderr(err),
        ):
            code = verify.main(["--changes", "--tested-sha", "abc", "--repository", REPOSITORY])
        self.assertEqual(code, 0)
        return out.getvalue(), err.getvalue()

    def test_a_tested_tree_prints_heavy_false_and_what_to_promote(self) -> None:
        out, _ = self.main(tested_tree.Tested(RUN, "h" * 40, ("dotnet",)))

        self.assertEqual(
            out.splitlines(),
            ["heavy=false", f"tested_run={RUN}", f"tested_head={'h' * 40}", "promote=dotnet"],
        )

    def test_an_untested_tree_prints_heavy_true_and_nothing_else_on_stdout(self) -> None:
        out, err = self.main(None)

        self.assertEqual(out.strip(), "heavy=true")
        self.assertIn("régua inteira", err)


if __name__ == "__main__":
    unittest.main()
