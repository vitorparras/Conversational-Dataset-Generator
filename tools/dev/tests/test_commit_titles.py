"""The titles of the commits of a branch: Conventional Commits, at most 72 characters."""

from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

import commit_titles

from . import support


class ProblemsTests(unittest.TestCase):
    def test_a_conventional_title_has_no_problem(self) -> None:
        self.assertEqual([], commit_titles.problems(["feat(dev): a verificação local (#808)"]))

    def test_a_title_of_one_hundred_characters_is_over_the_ceiling(self) -> None:
        title = "feat: " + "x" * 94

        found = commit_titles.problems([title])

        self.assertEqual(1, len(found))
        self.assertIn("100 caracteres", found[0])

    def test_a_title_without_type_is_outside_the_format(self) -> None:
        found = commit_titles.problems(["ajusta uma coisa"])

        self.assertIn("Conventional Commits", found[0])


class RunTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.repo = support.repository(Path(self._tmp.name))
        support.git(self.repo, "checkout", "-q", "-b", "feature")

    def test_a_branch_with_a_title_of_one_hundred_characters_fails(self) -> None:
        support.commit(self.repo, {"a.txt": "a"}, "feat: " + "y" * 94)

        result = commit_titles.run(support.context(self.repo))

        self.assertFalse(result.ok)
        self.assertIn("100 caracteres", result.details[0])

    def test_a_branch_with_good_titles_passes(self) -> None:
        support.commit(self.repo, {"a.txt": "a"}, "fix(dev): corrige o escopo")

        self.assertTrue(commit_titles.run(support.context(self.repo)).ok)

    def test_a_merge_commit_is_not_judged(self) -> None:
        support.commit(self.repo, {"a.txt": "a"}, "fix: um")
        support.git(self.repo, "checkout", "-q", "main")
        support.commit(self.repo, {"b.txt": "b"}, "chore: outro")
        support.git(self.repo, "checkout", "-q", "feature")
        support.git(self.repo, "merge", "--no-ff", "-q", "main", "-m", "Merge branch 'main'")

        result = commit_titles.run(support.context(self.repo))

        self.assertTrue(result.ok, result.details)
