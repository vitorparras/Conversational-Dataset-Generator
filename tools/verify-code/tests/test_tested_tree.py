"""Tests for the push to the main: it skips the heavy jobs only for a tree a green PR run recorded.

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
HEAD = "h" * 40
DOTNET = "Núcleo (.NET) e ferramentas"
PYTHON = "Serviço de linguagem (Python)"
AGGREGATE = "Mutação nos módulos críticos"
DOCS = "Verificações de documentação"
PATH = ".github/workflows/verify-code.yml@refs/pull/7/merge"


def tree_of(repo: Path, rev: str) -> str:
    return support.git_output(repo, "rev-parse", f"{rev}^{{tree}}")


def commit_file(repo: Path, name: str, message: str) -> str:
    support.write(repo, name, "x\n")
    support.git(repo, "add", ".")
    support.git(repo, "commit", "-q", "-m", message)
    return support.git_output(repo, "rev-parse", "HEAD")


class Github:
    """What the API says about a run that recorded ``tree``; git commands run for real."""

    def __init__(self, tree: str) -> None:
        self.recorded = {tested_tree.TREE_ARTIFACT.format(tree=tree): [RUN]}
        self.expired_tree = False
        self.runs = {RUN: self.run(RUN)}
        self.required = [AGGREGATE, DOCS]
        self.checks: dict[str, str | None] = {AGGREGATE: "success", DOCS: "success"}
        self.jobs = [
            {"name": DOTNET, "conclusion": "success"},
            {"name": PYTHON, "conclusion": "success"},
        ]
        self.kept = [f"cobertura-pr-dotnet-{HEAD}", f"cobertura-pr-python-{HEAD}"]
        self.expired_tables = False
        self.broken: set[str] = set()

    @staticmethod
    def run(number: int, conclusion: str | None = "success") -> dict[str, Any]:
        return {
            "id": number,
            "event": "pull_request",
            "status": "completed" if conclusion else "in_progress",
            "conclusion": conclusion,
            "path": PATH,
            "head_sha": HEAD,
        }

    def __call__(self, command: Sequence[str], cwd: Path) -> CommandOutcome:
        if command[0] != "gh":
            return run_process(command, cwd)
        path = command[2]
        if any(part in path for part in self.broken):
            return CommandOutcome(False, "HTTP 502", 1)
        return CommandOutcome(True, json.dumps(self.answer(path)), 0)

    def answer(self, path: str) -> object:
        if "/rules/" in path:
            checks = [{"context": name} for name in self.required]
            return [
                {"type": "required_status_checks", "parameters": {"required_status_checks": checks}}
            ]
        if "/check-runs" in path:
            return {"check_runs": [{"name": n, "conclusion": c} for n, c in self.checks.items()]}
        if "actions/artifacts?name=" in path:
            ids = self.recorded.get(path.split("name=")[1].split("&", maxsplit=1)[0], [])
            return {
                "artifacts": [
                    {"expired": self.expired_tree, "workflow_run": {"id": i}} for i in ids
                ]
            }
        if "/jobs" in path:
            return {"jobs": self.jobs}
        if "/artifacts" in path:
            return {"artifacts": [{"name": n, "expired": self.expired_tables} for n in self.kept]}
        return self.runs[int(path.split("/runs/")[1])]


class TestedTreeTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.repo = support.git_repository(Path(self._tmp.name))
        commit_file(self.repo, "docs/a.md", "head")
        self.tested_tree = tree_of(self.repo, "HEAD")
        support.git(self.repo, "checkout", "-q", "main")
        support.git(self.repo, "merge", "-q", "--squash", "change")
        support.git(self.repo, "commit", "-q", "-m", "squash (#7)")
        self.github = Github(self.tested_tree)

    def decide(self) -> tested_tree.Tested | None:
        ctx = Context(self.repo, self.repo / ".o", self.github)
        return tested_tree.tested(ctx, REPOSITORY, "HEAD")

    def test_a_tree_a_green_run_recorded_skips_and_promotes_the_tables_that_run_made(self) -> None:
        self.assertEqual(self.decide(), tested_tree.Tested(RUN, HEAD, ("dotnet", "python")))

    def test_a_run_that_skipped_the_heavy_jobs_skips_and_promotes_nothing(self) -> None:
        self.github.jobs = [{"name": DOTNET, "conclusion": "skipped"}]
        self.github.kept = []
        self.github.checks = {AGGREGATE: "success", DOCS: "skipped"}

        self.assertEqual(self.decide(), tested_tree.Tested(RUN, HEAD, ()))

    def test_a_stacked_pull_request_redirected_to_the_main_without_a_new_push_runs_everything(
        self,
    ) -> None:
        support.git(self.repo, "checkout", "-q", "-b", "base-of-the-stack", "main~1")
        commit_file(self.repo, "core/base.cs", "the stack below")
        support.git(self.repo, "checkout", "-q", "-b", "stacked")
        commit_file(self.repo, "docs/a.md", "the pull request on top")
        recorded = tree_of(self.repo, "HEAD")
        self.github = Github(recorded)
        support.git(self.repo, "checkout", "-q", "main")

        self.assertNotEqual(recorded, tree_of(self.repo, "HEAD"))
        self.assertEqual(self.github.recorded, {f"arvore-{recorded}": [RUN]})
        self.assertIsNone(self.decide())

    def test_a_red_run_that_recorded_the_same_tree_does_not_release(self) -> None:
        for conclusion in ("failure", "cancelled", None):
            with self.subTest(conclusion=conclusion):
                self.github.runs = {RUN: Github.run(RUN, conclusion)}

                self.assertIsNone(self.decide())

    def test_a_green_run_releases_even_if_an_older_run_of_the_same_tree_was_red(self) -> None:
        self.github.recorded = {f"arvore-{self.tested_tree}": [9, RUN]}
        self.github.runs = {9: Github.run(9, "failure"), RUN: Github.run(RUN)}

        self.assertEqual(self.decide(), tested_tree.Tested(RUN, HEAD, ("dotnet", "python")))

    def test_a_required_check_that_is_not_green_does_not_release(self) -> None:
        for conclusion in ("failure", "cancelled", "timed_out", None):
            with self.subTest(conclusion=conclusion):
                self.github.checks[DOCS] = conclusion

                self.assertIsNone(self.decide())

    def test_a_required_check_that_never_reported_does_not_release(self) -> None:
        del self.github.checks[DOCS]

        self.assertIsNone(self.decide())

    def test_a_ruleset_that_cannot_be_read_does_not_release(self) -> None:
        self.github.broken = {"/rules/"}

        self.assertIsNone(self.decide())

    def test_a_ruleset_with_no_required_check_does_not_release(self) -> None:
        self.github.required = []

        self.assertIsNone(self.decide())

    def test_a_tree_nobody_recorded_runs_everything(self) -> None:
        self.github.recorded = {}

        self.assertIsNone(self.decide())

    def test_an_expired_record_runs_everything(self) -> None:
        self.github.expired_tree = True

        self.assertIsNone(self.decide())

    def test_a_run_of_another_event_or_workflow_runs_everything(self) -> None:
        for change in ({"event": "push"}, {"path": ".github/workflows/codeql.yml@refs/heads/main"}):
            with self.subTest(change=change):
                self.github.runs = {RUN: {**Github.run(RUN), **change}}

                self.assertIsNone(self.decide())

    def test_a_table_the_run_should_have_kept_but_did_not_runs_everything(self) -> None:
        self.github.kept = [f"cobertura-pr-dotnet-{HEAD}"]

        self.assertIsNone(self.decide())

    def test_an_expired_table_runs_everything(self) -> None:
        self.github.expired_tables = True

        self.assertIsNone(self.decide())

    def test_any_failure_to_ask_runs_everything(self) -> None:
        for part in (
            "actions/artifacts?name=",
            "/runs/4242",
            "/check-runs",
            "/jobs",
            "/artifacts?per",
        ):
            with self.subTest(part=part):
                self.github.broken = {part}

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
