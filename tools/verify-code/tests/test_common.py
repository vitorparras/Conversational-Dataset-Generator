"""Tests for what the steps share: the process runner, the output tail and the child environment."""

from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from steps import common


class CommonTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)

    def test_missing_executable_is_a_failed_outcome_not_an_exception(self) -> None:
        outcome = common.run_process(["zqx-marker-7731-no-such-tool"], self.root)

        self.assertFalse(outcome.ok)
        self.assertIn("não está no PATH", outcome.text)

    def test_child_environment_adds_the_telemetry_opt_out(self) -> None:
        environment = common.child_environment({"OTHER": "kept"})

        self.assertEqual(environment, {"OTHER": "kept", "DOTNET_CLI_TELEMETRY_OPTOUT": "1"})

    def test_tail_keeps_only_the_last_lines_indented(self) -> None:
        text = "\n".join(str(n) for n in range(40))

        lines = common.tail(text)

        self.assertEqual(len(lines), common.OUTPUT_TAIL_LINES)
        self.assertEqual(lines[-1], "  | 39")

    def test_reset_directory_discards_earlier_content(self) -> None:
        target = self.root / "out"
        target.mkdir()
        (target / "stale.txt").write_text("old", encoding="utf-8")

        common.reset_directory(target)

        self.assertEqual(list(target.iterdir()), [])
