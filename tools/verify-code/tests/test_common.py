"""Tests for what the steps share: the process runner, the output tail and the child environment."""

from __future__ import annotations

import os
import subprocess
import sys
import tempfile
import time
import unittest
from pathlib import Path

from steps import common


def _is_alive(pid: int) -> bool:
    """Whether a process with this pid is still running (a zombie counts as gone)."""
    if os.name == "nt":
        listing = subprocess.run(
            ["tasklist", "/FI", f"PID eq {pid}", "/NH"], capture_output=True, text=True, check=False
        )
        return str(pid) in listing.stdout
    try:
        os.kill(pid, 0)
    except ProcessLookupError:
        return False
    stat = Path(f"/proc/{pid}/stat")
    return not (
        stat.exists() and stat.read_text(encoding="utf-8").rsplit(")", 1)[1].split()[0] == "Z"
    )


class CommonTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)

    def test_missing_executable_is_a_failed_outcome_not_an_exception(self) -> None:
        outcome = common.run_process(["zqx-marker-7731-no-such-tool"], self.root)

        self.assertFalse(outcome.ok)
        self.assertIn("não está no PATH", outcome.text)

    def test_a_command_past_its_limit_is_cut_and_says_so(self) -> None:
        sleeper = [sys.executable, "-c", "import time; time.sleep(30)"]

        outcome = common.run_process(sleeper, self.root, 1.0)

        self.assertFalse(outcome.ok)
        self.assertIn("passou de 1 s e foi interrompido", outcome.text)

    def test_a_command_past_its_limit_leaves_no_child_or_grandchild_alive(self) -> None:
        pids = self.root / "pids.txt"
        script = (
            "import os, pathlib, subprocess, sys, time\n"
            "grandchild = subprocess.Popen([sys.executable, '-c', 'import time; time.sleep(60)'])\n"
            f"pathlib.Path({str(pids)!r}).write_text(f'{{os.getpid()}} {{grandchild.pid}}')\n"
            "time.sleep(60)\n"
        )

        started = time.monotonic()
        outcome = common.run_process([sys.executable, "-c", script], self.root, 4.0)
        elapsed = time.monotonic() - started

        self.assertFalse(outcome.ok)
        self.assertIn("passou de 4 s e foi interrompido", outcome.text)
        self.assertLess(elapsed, 30, "the run waited for the grandchild instead of cutting it")
        child, grandchild = (int(pid) for pid in pids.read_text(encoding="utf-8").split())
        self.assertFalse(_is_alive(child), "the child outlived the limit")
        self.assertFalse(_is_alive(grandchild), "the grandchild outlived the limit")

    def test_a_command_inside_its_limit_keeps_its_output_and_exit_code(self) -> None:
        script = "import sys; print('out'); print('err', file=sys.stderr); sys.exit(3)"

        outcome = common.run_process([sys.executable, "-c", script], self.root, 30.0)

        self.assertFalse(outcome.ok)
        self.assertEqual(outcome.code, 3)
        self.assertIn("out", outcome.text)
        self.assertIn("err", outcome.text)

    def test_a_context_runs_commands_with_a_limit_through_the_process_runner(self) -> None:
        context = common.Context(self.root, self.root, common.run_process)

        self.assertIs(context.run_for, common.run_process)

    def test_finished_command_carries_its_exit_code(self) -> None:
        outcome = common.run_process(["python3", "-c", "raise SystemExit(69)"], self.root)

        self.assertFalse(outcome.ok)
        self.assertEqual(outcome.code, 69)

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
