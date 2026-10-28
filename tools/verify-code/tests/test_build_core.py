"""Tests for the build_core and format_core steps, with a fake runner in place of dotnet."""

from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from steps import build_core, format_core
from steps.common import CommandOutcome

from . import support


class BuildCoreTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)
        support.write(self.root, build_core.SOLUTION, support.SOLUTION_WITH_TESTS)

    def test_missing_solution_fails(self) -> None:
        (self.root / build_core.SOLUTION).unlink()

        result = build_core.run(support.context(self.root, support.FakeRunner()))

        self.assertFalse(result.ok)
        self.assertIn("não há o que construir", result.details[0])

    def test_failed_locked_restore_fails_naming_the_lock(self) -> None:
        runner = support.FakeRunner(CommandOutcome(False, "NU1004: lock file out of date"))

        result = build_core.run(support.context(self.root, runner))

        self.assertFalse(result.ok)
        self.assertIn("lock desatualizado", result.details[0])
        self.assertIn("  | NU1004: lock file out of date", result.details)

    def test_restore_runs_in_locked_mode_before_the_build(self) -> None:
        runner = support.FakeRunner()

        build_core.run(support.context(self.root, runner))

        self.assertEqual(runner.commands[0][:3], ["dotnet", "restore", build_core.SOLUTION])
        self.assertIn("--locked-mode", runner.commands[0])

    def test_restore_resolves_again_so_an_old_obj_folder_cannot_hide_a_stale_lock(self) -> None:
        runner = support.FakeRunner()

        build_core.run(support.context(self.root, runner))

        self.assertIn("--force", runner.commands[0])
        self.assertEqual(runner.commands[1][:2], ["dotnet", "build"])

    def test_restore_and_build_succeeding_passes(self) -> None:
        result = build_core.run(support.context(self.root, support.FakeRunner()))

        self.assertTrue(result.ok)

    def test_format_divergence_fails_and_says_how_to_fix(self) -> None:
        runner = support.FakeRunner(CommandOutcome(False, "Program.cs(3,1): error WHITESPACE"))

        result = format_core.run(support.context(self.root, runner))

        self.assertFalse(result.ok)
        self.assertIn("rode `dotnet format core/Core.slnx`", result.details[0])

    def test_format_verifies_without_changing_files(self) -> None:
        runner = support.FakeRunner()

        result = format_core.run(support.context(self.root, runner))

        self.assertTrue(result.ok)
        self.assertIn("--verify-no-changes", runner.commands[0])
