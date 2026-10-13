"""Tests for the test_tools step, run for real against a synthetic tools/ directory."""

from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from steps import test_tools

from . import support

PASSING_TEST = (
    "import unittest\n\nclass T(unittest.TestCase):\n    def test_ok(self) -> None:\n        pass\n"
)
FAILING_TEST = PASSING_TEST.replace(
    "def test_ok(self) -> None:\n        pass", "def test_no(self) -> None:\n        self.fail()"
)


class TestToolsTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)
        (self.root / test_tools.TOOLS).mkdir()

    def test_missing_tools_directory_fails(self) -> None:
        (self.root / test_tools.TOOLS).rmdir()

        result = test_tools.run(support.context(self.root))

        self.assertFalse(result.ok)
        self.assertIn("não há suíte", result.details[0])

    def test_directory_without_tests_fails_instead_of_passing_empty(self) -> None:
        result = test_tools.run(support.context(self.root))

        self.assertFalse(result.ok)
        self.assertIn("nenhum teste encontrado em tools/: o passo não passa vazio", result.details)

    def test_a_passing_test_passes_with_its_count(self) -> None:
        support.write(self.root, "tools/test_sample.py", PASSING_TEST)

        result = test_tools.run(support.context(self.root))

        self.assertTrue(result.ok, result.details)
        self.assertIn("1 testes de tools/ executados", result.details)

    def test_a_failing_test_fails_the_step(self) -> None:
        support.write(self.root, "tools/test_sample.py", FAILING_TEST)

        result = test_tools.run(support.context(self.root))

        self.assertFalse(result.ok)
        self.assertIn("testes de tools/ falharam", result.details)
