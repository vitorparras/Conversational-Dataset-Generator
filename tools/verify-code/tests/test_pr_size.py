"""Tests for the pr_size step against a synthetic git repository built in a temporary directory."""

from __future__ import annotations

import os
import tempfile
import unittest
from collections.abc import Callable, Sequence
from pathlib import Path
from unittest import mock

from steps import pr_size
from steps.common import CommandOutcome, run_process

from . import support

CLOSING = {"CDG_PR_BODY": "Closes #12"}


def with_issue(text: str | None) -> Callable[[Sequence[str], Path], CommandOutcome]:
    """Runs git for real; `gh issue view` answers with the issue text, or fails when it is None."""

    def run(command: Sequence[str], cwd: Path) -> CommandOutcome:
        if command[0] != "gh":
            return run_process(command, cwd)
        return CommandOutcome(text is not None, text or "")

    return run


class PrSizeTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.repo = support.git_repository(Path(self._tmp.name))
        environment = mock.patch.dict(os.environ, {"CDG_PR_BODY": ""})
        environment.start()
        self.addCleanup(environment.stop)

    def run_with(self, issue: str | None, env: dict[str, str] = CLOSING) -> pr_size.StepResult:
        with mock.patch.dict(os.environ, env):
            return pr_size.run(support.context(self.repo, with_issue(issue), base="main"))

    def test_many_small_files_pass_because_there_is_no_file_ceiling(self) -> None:
        support.commit_files(self.repo, support.numbered_files("src/file_", 60), 1)

        result = pr_size.run(support.context(self.repo, base="main"))

        self.assertTrue(result.ok, result.details)
        self.assertIn(
            "produção: 60 arquivos, 60 linhas (teto 400 linhas, o padrão)", result.details
        )

    def test_four_hundred_and_one_production_lines_fail_naming_the_number(self) -> None:
        support.commit_files(self.repo, ["src/a.cs"], 401)

        result = pr_size.run(support.context(self.repo, base="main"))

        self.assertFalse(result.ok)
        self.assertIn("401 linhas de produção passam do teto de 400", result.details)

    def test_four_hundred_lines_pass_under_the_default_ceiling(self) -> None:
        support.commit_files(self.repo, ["src/big.cs"], 400)

        result = pr_size.run(support.context(self.repo, base="main"))

        self.assertTrue(result.ok, result.details)
        self.assertIn(
            "produção: 1 arquivos, 400 linhas (teto 400 linhas, o padrão)", result.details
        )

    def test_tests_locks_and_fixtures_are_not_counted(self) -> None:
        excluded = [
            "core/tests/A/A.cs",
            "service/tests/t.py",
            "core/src/packages.lock.json",
            "service/uv.lock",
            "tools/x/fixtures/f.md",
        ]
        support.commit_files(self.repo, excluded, 500)

        result = pr_size.run(support.context(self.repo, base="main"))

        self.assertTrue(result.ok, result.details)
        self.assertIn("produção: 0 arquivos, 0 linhas (teto 400 linhas, o padrão)", result.details)

    def test_excluded_files_are_listed_with_their_lines(self) -> None:
        support.commit_files(self.repo, ["core/tests/A/A.cs"], 7)

        result = pr_size.run(support.context(self.repo, base="main"))

        self.assertIn("fora da conta: 1 arquivos", result.details)
        self.assertIn("  - core/tests/A/A.cs (7 linhas)", result.details)

    def test_without_a_base_the_step_fails(self) -> None:
        result = pr_size.run(support.context(self.repo))

        self.assertFalse(result.ok)
        self.assertIn("--base", result.details[0])

    def test_unknown_base_fails(self) -> None:
        result = pr_size.run(support.context(self.repo, base="no-such-ref"))

        self.assertFalse(result.ok)
        self.assertIn("falhou", result.details[0])

    def test_a_binary_file_counts_zero_lines(self) -> None:
        changed = pr_size.parse_numstat("-\t-\tsrc/image.png\n3\t1\tsrc/a.cs\n")

        self.assertEqual((changed[0].lines, changed[1].lines), (0, 4))

    def test_the_ceiling_is_read_from_the_issue_the_pull_request_closes(self) -> None:
        support.commit_files(self.repo, ["src/a.cs"], 450)

        result = self.run_with("Contexto\n\nTeto da fatia: 500 linhas\nOutra linha")

        self.assertTrue(result.ok, result.details)
        self.assertIn(
            "produção: 1 arquivos, 450 linhas (teto 500 linhas, da issue #12)", result.details
        )

    def test_a_line_over_the_issue_ceiling_fails_naming_both_numbers(self) -> None:
        support.commit_files(self.repo, ["src/a.cs"], 301)

        result = self.run_with("Teto da fatia: 300 linhas")

        self.assertFalse(result.ok)
        self.assertIn("301 linhas de produção passam do teto de 300", result.details)

    def test_the_issue_number_comes_from_the_branch_when_the_body_has_none(self) -> None:
        support.git(self.repo, "checkout", "-q", "-b", "feat/77-fatia")
        support.commit_files(self.repo, ["src/a.cs"], 5)

        result = self.run_with("Teto da fatia: 900 linhas", env={})

        self.assertIn("(teto 900 linhas, da issue #77)", result.details[0])

    def test_a_ceiling_over_the_hard_limit_fails_even_when_the_lines_fit(self) -> None:
        support.commit_files(self.repo, ["src/a.cs"], 5)

        result = self.run_with("Teto da fatia: 1.501 linhas")

        self.assertFalse(result.ok)
        self.assertIn(
            "o teto de 1501 linhas da issue #12 passa do limite duro de 1500", result.details
        )

    def test_the_hard_limit_itself_is_accepted(self) -> None:
        support.commit_files(self.repo, ["src/a.cs"], 5)

        result = self.run_with("Teto da fatia: 1.500 linhas")

        self.assertTrue(result.ok, result.details)

    def test_an_issue_without_the_ceiling_line_keeps_the_default(self) -> None:
        support.commit_files(self.repo, ["src/a.cs"], 401)

        result = self.run_with("sem a linha")

        self.assertIn("401 linhas de produção passam do teto de 400", result.details)

    def test_an_issue_that_cannot_be_read_keeps_the_default_and_says_so(self) -> None:
        support.commit_files(self.repo, ["src/a.cs"], 5)

        result = self.run_with(None)

        self.assertTrue(result.ok, result.details)
        self.assertIn("(teto 400 linhas, o padrão: a issue #12 não foi lida)", result.details[0])

    def test_closes_is_found_in_any_case_and_the_first_issue_wins(self) -> None:
        self.assertEqual("8", pr_size.issue_number("texto\nCLOSES #8 e closes #9", ""))

    def test_fecha_does_not_name_an_issue_and_main_has_no_number(self) -> None:
        self.assertIsNone(pr_size.issue_number("Fecha #8", "main"))
