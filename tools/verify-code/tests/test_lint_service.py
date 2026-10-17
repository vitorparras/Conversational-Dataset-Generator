"""Tests for the lint_service step: the service folder, its venv, and the three commands."""

from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from steps import lint_service
from steps.common import CommandOutcome

from . import support


class LintServiceTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)
        (self.root / lint_service.VENV).mkdir(parents=True)

    def test_missing_service_folder_fails(self) -> None:
        (self.root / lint_service.VENV).rmdir()
        (self.root / lint_service.SERVICE).rmdir()

        result = lint_service.run(support.context(self.root, support.FakeRunner()))

        self.assertFalse(result.ok)
        self.assertIn("não há o que verificar", result.details[0])

    def test_missing_venv_fails_naming_the_step_that_creates_it(self) -> None:
        (self.root / lint_service.VENV).rmdir()

        result = lint_service.run(support.context(self.root, support.FakeRunner()))

        self.assertFalse(result.ok)
        self.assertIn("rode o passo build_service antes", result.details[0])

    def test_a_failing_command_fails_with_the_remedy_for_the_service_folder(self) -> None:
        runner = support.FakeRunner(CommandOutcome(False, "Would reformat: app.py"))

        result = lint_service.run(support.context(self.root, runner))

        self.assertFalse(result.ok)
        self.assertIn(
            "`ruff format --check language-service` reprovou: rode `ruff format language-service`",
            result.details[0],
        )

    def test_the_three_commands_passing_passes(self) -> None:
        runner = support.FakeRunner()

        result = lint_service.run(support.context(self.root, runner))

        self.assertTrue(result.ok, result.details)
        self.assertEqual(runner.commands[0][2:], ["ruff", "format", "--check", "language-service"])
        self.assertEqual(runner.commands[1][2:], ["ruff", "check", "language-service"])
        self.assertEqual(runner.commands[2][2:], ["pyright", "language-service"])
