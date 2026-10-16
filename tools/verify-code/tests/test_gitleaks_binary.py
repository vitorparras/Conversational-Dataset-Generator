"""Tests for the pinned gitleaks binary: lookup on the PATH and the SHA-256 check."""

from __future__ import annotations

import re
import sys
import tempfile
import unittest
from unittest import mock
from pathlib import Path

from steps import gitleaks_binary

from . import support

ACTION = support.VERIFY_CODE.parents[1] / ".github" / "actions" / "install-gitleaks" / "action.yml"


class LocateTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.folder = Path(self._tmp.name)

    def test_a_folder_without_the_binary_fails_naming_the_installation(self) -> None:
        located = gitleaks_binary.locate(str(self.folder))

        self.assertIsNone(located.path)
        self.assertIn("'gitleaks' não está no PATH", located.problem)
        self.assertIn(gitleaks_binary.VERSION, located.problem)
        self.assertIn(gitleaks_binary.RELEASE_URL, located.problem)

    def test_a_binary_with_another_hash_is_refused_and_the_expected_hash_is_named(self) -> None:
        binary = self.folder / ("gitleaks.exe" if sys.platform == "win32" else "gitleaks")
        binary.write_bytes(b"ZQX-MARKER-7731 not the pinned release")
        binary.chmod(0o755)

        located = gitleaks_binary.locate(str(self.folder))

        self.assertIsNone(located.path)
        self.assertIn("não é o fixado", located.problem)
        self.assertIn(gitleaks_binary.PINS[sys.platform].sha256, located.problem)

    def test_another_platform_fails_naming_the_supported_ones(self) -> None:
        with mock.patch.object(sys, "platform", "darwin"):
            located = gitleaks_binary.locate(str(self.folder))

        self.assertIsNone(located.path)
        self.assertIn("só para linux, win32; plataforma darwin", located.problem)

    def test_the_binary_on_the_path_is_the_pinned_one(self) -> None:
        located = gitleaks_binary.locate()

        self.assertEqual(located.problem, "")
        self.assertIsNotNone(located.path)


class PinTests(unittest.TestCase):
    def test_the_install_action_downloads_the_pinned_linux_archive(self) -> None:
        text = ACTION.read_text(encoding="utf-8")

        self.assertIn(
            f"{gitleaks_binary.RELEASE_URL}/{gitleaks_binary.PINS['linux'].archive}", text
        )

    def test_every_pinned_hash_is_a_sha256_digest(self) -> None:
        digests = [pin.sha256 for pin in gitleaks_binary.PINS.values()]

        self.assertEqual([d for d in digests if not re.fullmatch(r"[0-9a-f]{64}", d)], [])
