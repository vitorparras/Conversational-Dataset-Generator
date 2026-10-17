"""Tests for the generic lookup of a pinned tool, with a synthetic tool that is not gitleaks."""

from __future__ import annotations

import sys
import tempfile
import unittest
from pathlib import Path

from steps import pinned_tool
from steps.pin import Pin
from steps.pinned_tool import PinnedTool

CONTENT = b"ZQX-MARKER-7731 synthetic tool"
BINARY_NAME = "zqxtool.exe" if sys.platform == "win32" else "zqxtool"


class LocateToolTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.folder = Path(self._tmp.name)
        self.binary = self.folder / BINARY_NAME
        self.binary.write_bytes(CONTENT)
        self.binary.chmod(0o755)

    def tool(self, sha256: str) -> PinnedTool:
        pins = {sys.platform: Pin("zqxtool.tar.gz", sha256)}
        return PinnedTool("zqxtool", "1.2.3", "https://example.invalid/releases", pins)

    def test_a_binary_with_the_pinned_hash_is_located(self) -> None:
        located = pinned_tool.locate(
            self.tool(pinned_tool.sha256_of(self.binary)), str(self.folder)
        )

        self.assertEqual(located.problem, "")
        self.assertEqual(Path(located.path or "").name.lower(), BINARY_NAME)

    def test_a_binary_with_another_hash_is_refused_naming_the_tool_and_the_version(self) -> None:
        located = pinned_tool.locate(self.tool("0" * 64), str(self.folder))

        self.assertIsNone(located.path)
        self.assertIn("o zqxtool encontrado não é o fixado (1.2.3)", located.problem)

    def test_the_tool_is_searched_by_its_own_name(self) -> None:
        self.binary.rename(self.folder / BINARY_NAME.replace("zqxtool", "other"))

        located = pinned_tool.locate(self.tool("0" * 64), str(self.folder))

        self.assertIn("'zqxtool' não está no PATH", located.problem)
        self.assertIn("https://example.invalid/releases/zqxtool.tar.gz", located.problem)
