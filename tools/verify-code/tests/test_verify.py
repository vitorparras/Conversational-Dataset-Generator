"""Tests for tools/verify-code/verify.py: step order, selection, report and exit codes."""

from __future__ import annotations

import contextlib
import io
import tempfile
import time
import unittest
from collections.abc import Sequence
from itertools import takewhile
from pathlib import Path

from steps.common import Context, StepResult, failed, passed

from . import support

verify = support.load_verify()
WORKFLOW = support.VERIFY_CODE.parents[1] / ".github" / "workflows" / "verify-code.yml"


def steps_in_workflow(text: str) -> list[str]:
    """The step names after `--step` in every `run:` line that calls verify.py, in file order."""
    names: list[str] = []
    for line in text.splitlines():
        if "verify.py" not in line or "--step" not in line:
            continue
        after = line.split("--step", 1)[1].split()
        names.extend(takewhile(lambda token: not token.startswith("--"), after))
    return names


def pr_size_line(text: str) -> str:
    """The `run:` line of the workflow that calls the pr_size step."""
    return next(line for line in text.splitlines() if "--step pr_size" in line)


def names(specs: Sequence[object]) -> list[str]:
    return [str(getattr(spec, "name")) for spec in specs]


def good(_ctx: Context) -> StepResult:
    return passed("good", "fine")


def bad(_ctx: Context) -> StepResult:
    return failed("bad", "broke")


def slow(_ctx: Context) -> StepResult:
    time.sleep(0.01)
    return passed("slow")


def run_main(*argv: str) -> tuple[int, str]:
    buffer = io.StringIO()
    with contextlib.redirect_stdout(buffer):
        code = verify.main(list(argv))
    return code, buffer.getvalue()


class VerifyTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)

    def test_list_prints_the_steps_in_the_order_of_the_workflow(self) -> None:
        expected = steps_in_workflow(WORKFLOW.read_text(encoding="utf-8"))

        code, output = run_main("--list")

        self.assertEqual(code, 0)
        self.assertEqual(output.split(), expected)

    def test_workflow_measures_pr_size_against_the_base_of_the_pull_request(self) -> None:
        line = pr_size_line(WORKFLOW.read_text(encoding="utf-8"))

        self.assertIn("--base origin/${{ github.base_ref }}", line)

    def test_unknown_step_is_a_usage_error(self) -> None:
        code, output = run_main("--step", "no_such_step")

        self.assertEqual(code, 2)
        self.assertIn("no_such_step", output)

    def test_default_selection_leaves_out_the_steps_that_need_a_base(self) -> None:
        selected = verify.select([], False)

        self.assertEqual(names(selected), ["build_core", "test_core", "format_core", "test_tools"])

    def test_default_selection_with_a_base_includes_pr_size(self) -> None:
        selected = verify.select([], True)

        self.assertIn("pr_size", names(selected))

    def test_requested_steps_run_in_ci_order_not_in_request_order(self) -> None:
        selected = verify.select(["test_tools", "build_core"], False)

        self.assertEqual(names(selected), ["build_core", "test_tools"])

    def test_a_failing_step_fails_the_run_and_is_named(self) -> None:
        specs = (verify.StepSpec("good", good), verify.StepSpec("bad", bad))
        ctx = support.context(self.root)
        buffer = io.StringIO()

        with contextlib.redirect_stdout(buffer):
            code = verify.report(verify.run_steps(specs, ctx))

        self.assertEqual(code, 1)
        self.assertIn("FALHA  bad", buffer.getvalue())
        self.assertIn("broke", buffer.getvalue())

    def test_all_steps_passing_exit_zero(self) -> None:
        specs = (verify.StepSpec("good", good),)
        ctx = support.context(self.root)
        buffer = io.StringIO()

        with contextlib.redirect_stdout(buffer):
            code = verify.report(verify.run_steps(specs, ctx))

        self.assertEqual(code, 0)
        self.assertIn("OK — 1 passos", buffer.getvalue())

    def test_run_steps_records_the_elapsed_time_of_each_step(self) -> None:
        results = verify.run_steps((verify.StepSpec("slow", slow),), support.context(self.root))

        self.assertGreater(results[0].elapsed, 0.0)
