"""check.main with the steps replaced: exit code, 40 lines and the stop after a broken build."""

from __future__ import annotations

import contextlib
import io
import os
import tempfile
import unittest
from collections.abc import Callable
from pathlib import Path
from unittest import mock

import check

import dotnet_steps
from steps.common import Context, StepResult, failed, passed

from . import support


def fake(result: StepResult) -> Callable[..., StepResult]:
    return lambda *_args: result


class CheckMainTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        repo = Path(self._tmp.name) / "repo"
        repo.mkdir()
        self.repo = support.repository(repo)
        self.lock = Path(self._tmp.name) / "maquina.lock"
        for patcher in (
            mock.patch.object(check, "ROOT", self.repo),
            mock.patch.dict(os.environ, {"CDG_MACHINE_LOCK": str(self.lock)}),
        ):
            patcher.start()
            self.addCleanup(patcher.stop)
        self.titles = passed("titulos", "ok")
        self.size = passed("pr_size", "ok")
        self.docs = passed("docs", "ok")
        self.dotnet = [passed("build", "ok")]

    def run_main(self, *argv: str) -> tuple[int, list[str]]:
        out = io.StringIO()

        def dotnet(_ctx: Context) -> list[StepResult]:
            return self.dotnet

        with (
            mock.patch.object(check.commit_titles, "run", fake(self.titles)),
            mock.patch.object(check.pr_size, "run", fake(self.size)),
            mock.patch.object(check, "docs", fake(self.docs)),
            mock.patch.object(check, "dotnet_run", dotnet),
            contextlib.redirect_stdout(out),
        ):
            code = check.main(["--base", "main", *argv])
        return code, out.getvalue().splitlines()

    def test_every_step_passing_exits_zero(self) -> None:
        code, lines = self.run_main()

        self.assertEqual(0, code)
        self.assertIn("tudo certo", lines[-1])

    def test_one_failed_step_exits_non_zero_and_names_it(self) -> None:
        self.docs = failed("docs", "verify-docs reprovou:")

        code, lines = self.run_main()

        self.assertEqual(1, code)
        self.assertIn("reprovado em docs", lines[-1])

    def test_a_long_report_is_cut_to_40_lines_and_points_to_the_log(self) -> None:
        self.dotnet = [failed("build", *[f"erro {n}" for n in range(80)])]

        code, lines = self.run_main()

        self.assertEqual(1, code)
        self.assertEqual(check.REPORT_LINES, len(lines))
        self.assertIn(check.LOG, lines[-2])
        self.assertIn("reprovado em build", lines[-1])
        self.assertGreater(
            len((self.repo / check.LOG).read_text(encoding="utf-8").splitlines()), 80
        )

    def test_a_missing_base_fails_at_once_naming_the_fix(self) -> None:
        code, lines = self.run_main("--base", "origin/nada")

        self.assertEqual(1, code)
        self.assertIn("git fetch origin main", "\n".join(lines))

    def test_a_machine_that_stays_busy_becomes_a_failed_step(self) -> None:
        with mock.patch.object(
            check.machine_lock, "machine_lock", side_effect=TimeoutError("ocupada")
        ):
            code, lines = self.run_main()

        self.assertEqual(1, code)
        self.assertIn("reprovado em trava", lines[-1])


class DotnetRunTests(unittest.TestCase):
    def run_with(self, built: StepResult) -> list[StepResult]:
        calls: list[str] = []

        def step(name: str) -> Callable[..., StepResult]:
            def run(*_args: object) -> StepResult:
                calls.append(name)
                return passed(name, "ok")

            return run

        reach = mock.Mock(reached=("A",), everything=False)
        with (
            mock.patch.object(check.scope, "compute", return_value=reach),
            mock.patch.object(dotnet_steps, "build", fake(built)),
            mock.patch.object(dotnet_steps, "format_check", step(dotnet_steps.FORMAT)),
            mock.patch.object(dotnet_steps, "tests", step(dotnet_steps.TESTS)),
        ):
            results = check.dotnet_run(support.context(Path(".")))
        self.calls = calls
        return results

    def test_a_broken_build_stops_the_format_and_the_tests_and_fails_them(self) -> None:
        results = self.run_with(failed(dotnet_steps.BUILD, "A não compila:"))

        self.assertEqual([], self.calls)
        self.assertEqual([False, False, False], [r.ok for r in results])

    def test_a_good_build_runs_the_format_and_the_tests(self) -> None:
        results = self.run_with(passed(dotnet_steps.BUILD, "ok"))

        self.assertEqual([dotnet_steps.FORMAT, dotnet_steps.TESTS], self.calls)
        self.assertTrue(all(r.ok for r in results))
