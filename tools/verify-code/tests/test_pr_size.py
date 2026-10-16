"""Tests for the pr_size step against a synthetic git repository built in a temporary directory."""

from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from steps import pr_size

from . import support


class PrSizeTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.repo = support.git_repository(Path(self._tmp.name))

    def test_thirty_one_production_files_fail_naming_the_number(self) -> None:
        support.commit_files(self.repo, support.numbered_files("src/file_", 31), 1)

        result = pr_size.run(support.context(self.repo, base="main"))

        self.assertFalse(result.ok)
        self.assertIn("31 arquivos de produção passam do teto de 30", result.details)

    def test_four_hundred_and_one_production_lines_fail_naming_the_number(self) -> None:
        support.commit_files(self.repo, ["src/a.cs"], 401)

        result = pr_size.run(support.context(self.repo, base="main"))

        self.assertFalse(result.ok)
        self.assertIn("401 linhas de produção passam do teto de 400", result.details)

    def test_thirty_files_and_four_hundred_lines_pass(self) -> None:
        support.commit_files(self.repo, support.numbered_files("src/file_", 29), 1)
        support.commit_files(self.repo, ["src/big.cs"], 371)

        result = pr_size.run(support.context(self.repo, base="main"))

        self.assertTrue(result.ok, result.details)
        self.assertIn(
            "produção: 30 arquivos, 400 linhas (teto 30 arquivos, 400 linhas)", result.details
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
        self.assertIn(
            "produção: 0 arquivos, 0 linhas (teto 30 arquivos, 400 linhas)", result.details
        )

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
