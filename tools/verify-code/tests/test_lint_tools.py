"""Tests for the lint_tools step: a fake runner for the three commands, ruff for real once."""

from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from steps import lint_tools
from steps.common import CommandOutcome

from . import support


class LintToolsTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)
        (self.root / lint_tools.TOOLS).mkdir()

    def test_missing_tools_directory_fails(self) -> None:
        (self.root / lint_tools.TOOLS).rmdir()

        result = lint_tools.run(support.context(self.root, support.FakeRunner()))

        self.assertFalse(result.ok)
        self.assertIn("não há o que verificar", result.details[0])

    def test_a_missing_tool_fails_naming_the_tool_and_how_to_install_it(self) -> None:
        runner = support.FakeRunner(CommandOutcome(False, "No module named ruff"))

        result = lint_tools.run(support.context(self.root, runner))

        self.assertFalse(result.ok)
        self.assertIn("`ruff format --check tools` reprovou", result.details[0])
        self.assertIn(f"pip install -r {lint_tools.REQUIREMENTS}", result.details[1])

    def test_a_failing_command_fails_with_the_tail_of_its_output(self) -> None:
        runner = support.FakeRunner(CommandOutcome(False, "E501 Line too long (101 > 100)"))

        result = lint_tools.run(support.context(self.root, runner))

        self.assertFalse(result.ok)
        self.assertIn("  | E501 Line too long (101 > 100)", result.details)

    def test_the_three_commands_passing_passes(self) -> None:
        runner = support.FakeRunner()

        result = lint_tools.run(support.context(self.root, runner))

        self.assertTrue(result.ok, result.details)
        self.assertEqual(runner.commands[0][2:], ["ruff", "format", "--check", "tools"])
        self.assertEqual(runner.commands[1][2:], ["ruff", "check", "tools"])
        self.assertEqual(runner.commands[2][2:], ["pyright", "tools"])

    def test_a_badly_formatted_file_fails_with_ruff_for_real(self) -> None:
        support.write(self.root, "tools/sample.py", "x=1\n")

        result = lint_tools.run(support.context(self.root))

        self.assertFalse(result.ok)
        self.assertIn(
            "`ruff format --check tools` reprovou: rode `ruff format tools`", result.details
        )
