"""Tests for the build_service step, with a fake runner in place of uv."""

from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from steps import build_service
from steps.common import CommandOutcome

from . import support


class BuildServiceTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)
        support.write(self.root, build_service.PROJECT, support.PYPROJECT_WITH_TESTS)

    def test_missing_project_fails(self) -> None:
        (self.root / build_service.PROJECT).unlink()

        result = build_service.run(support.context(self.root, support.FakeRunner()))

        self.assertFalse(result.ok)
        self.assertIn("não há o que construir", result.details[0])

    def test_outdated_lock_fails_naming_the_lock_and_how_to_install_uv(self) -> None:
        text = "The lockfile at `uv.lock` needs to be updated, but `--locked` was provided."
        runner = support.FakeRunner(CommandOutcome(False, text))

        result = build_service.run(support.context(self.root, runner))

        self.assertFalse(result.ok)
        self.assertIn("lock desatualizado", result.details[0])
        self.assertIn(f"pip install -r {build_service.REQUIREMENTS}", result.details[1])
        self.assertIn(f"  | {text}", result.details)

    def test_sync_asserts_the_lock_inside_the_service_folder(self) -> None:
        runner = support.FakeRunner()

        build_service.run(support.context(self.root, runner))

        self.assertEqual(
            runner.commands[0], ["uv", "sync", "--locked", "--directory", build_service.SERVICE]
        )

    def test_sync_succeeding_passes(self) -> None:
        result = build_service.run(support.context(self.root, support.FakeRunner()))

        self.assertTrue(result.ok, result.details)
