"""Tests for tools/verify-code/verify.py: step order, selection, report and exit codes."""

from __future__ import annotations

import contextlib
import io
import tempfile
import time
import unittest
from collections.abc import Sequence
from itertools import chain, takewhile
from pathlib import Path
from typing import Any

from steps import build_service, lint_tools
from steps.common import Context, StepResult, failed, passed

from . import support

verify = support.load_verify()
WORKFLOW = support.VERIFY_CODE.parents[1] / ".github" / "workflows" / "verify-code.yml"
DOCS_WORKFLOW = support.VERIFY_CODE.parents[1] / ".github" / "workflows" / "verify-docs.yml"
WITHOUT_BASE = [
    "build_core",
    "test_core",
    "format_core",
    "lint_tools",
    "test_tools",
    "build_service",
    "test_service",
    "coverage",
    "lint_service",
    "properties",
    "secrets",
    "trivy",
]


def steps_per_job(text: str) -> list[list[str]]:
    """The step names after `--step` in each `run:` line that calls verify.py, in file order."""
    jobs: list[list[str]] = []
    for line in text.splitlines():
        if "verify.py" not in line or "--step" not in line:
            continue
        after = line.split("--step", 1)[1].split()
        jobs.append(list(takewhile(lambda token: not token.startswith(("--", "${{")), after)))
    return jobs


def out_of_order(jobs: list[list[str]], listed: list[str]) -> list[list[str]]:
    """The jobs whose steps are not in the order `--list` prints them."""
    return [job for job in jobs if job != [name for name in listed if name in job]]


def job_running(step: str, jobs: list[list[str]]) -> list[str]:
    """The steps of the first job that runs ``step``."""
    return next(job for job in jobs if step in job)


def pr_size_line(text: str) -> str:
    """The `run:` line of the workflow that calls the pr_size step."""
    return next(line for line in text.splitlines() if "--step pr_size" in line)


def line_after(text: str, marker: str) -> str:
    """The line that follows the first line equal to ``marker``."""
    lines = text.splitlines()
    return lines[lines.index(marker) + 1]


def names(specs: Sequence[Any]) -> list[str]:
    return [str(spec.name) for spec in specs]


def good(_ctx: Context) -> StepResult:
    return passed("good", "fine")


def bad(_ctx: Context) -> StepResult:
    return failed("bad", "broke")


SLEEP_SECONDS = 0.01


def slow(_ctx: Context) -> StepResult:
    time.sleep(SLEEP_SECONDS)
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

    def test_each_job_of_the_workflow_runs_its_steps_in_the_order_of_list(self) -> None:
        jobs = steps_per_job(WORKFLOW.read_text(encoding="utf-8"))

        code, output = run_main("--list")

        self.assertEqual(code, 0)
        self.assertEqual(out_of_order(jobs, output.split()), [])

    def test_every_listed_step_runs_in_some_job_of_the_workflow(self) -> None:
        jobs = steps_per_job(WORKFLOW.read_text(encoding="utf-8"))

        _, output = run_main("--list")

        self.assertEqual(set(chain.from_iterable(jobs)), set(output.split()))

    def test_coverage_runs_in_the_job_that_tests_the_core(self) -> None:
        jobs = steps_per_job(WORKFLOW.read_text(encoding="utf-8"))

        self.assertIn("coverage", job_running("test_core", jobs))

    def test_coverage_runs_in_the_job_that_tests_the_service(self) -> None:
        jobs = steps_per_job(WORKFLOW.read_text(encoding="utf-8"))

        self.assertIn("coverage", job_running("test_service", jobs))

    def test_workflow_measures_pr_size_against_the_base_of_the_pull_request(self) -> None:
        line = pr_size_line(WORKFLOW.read_text(encoding="utf-8"))

        self.assertIn("--base origin/${{ github.base_ref }}", line)

    def test_docs_workflow_runs_only_the_tests_of_the_docs_verifier(self) -> None:
        text = DOCS_WORKFLOW.read_text(encoding="utf-8")

        self.assertIn("unittest discover -s tools/verify-docs", text)
        self.assertNotIn("discover -s tools ", text)
        self.assertNotIn("verify-code", text)

    def test_workflow_runs_on_pull_requests_against_any_base(self) -> None:
        following = line_after(WORKFLOW.read_text(encoding="utf-8"), "  pull_request:")

        self.assertNotIn("branches", following)

    def test_workflow_installs_the_linters_from_the_file_the_step_names(self) -> None:
        text = WORKFLOW.read_text(encoding="utf-8")

        self.assertIn(f"pip install -r {lint_tools.REQUIREMENTS}", text)

    def test_workflow_installs_uv_from_the_file_the_build_service_step_names(self) -> None:
        text = WORKFLOW.read_text(encoding="utf-8")

        self.assertIn(f"pip install -r {build_service.REQUIREMENTS}", text)

    def test_unknown_step_is_a_usage_error(self) -> None:
        code, output = run_main("--step", "no_such_step")

        self.assertEqual(code, 2)
        self.assertIn("no_such_step", output)

    def test_default_selection_leaves_out_the_steps_that_need_a_base(self) -> None:
        selected = verify.select([], False)

        self.assertEqual(names(selected), WITHOUT_BASE)

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

        self.assertGreaterEqual(results[0].elapsed, SLEEP_SECONDS)
        self.assertLess(results[0].elapsed, SLEEP_SECONDS + 1)
