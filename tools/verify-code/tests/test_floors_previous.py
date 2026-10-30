"""Tests for the drops floors reports against the main's by-area.md: summary and annotation."""

from __future__ import annotations

import contextlib
import io
import os

from steps import coverage_by_area, floors

from . import floors_fixture as fixture
from . import support
from .floors_fixture import LOGGING, PREVIOUS, STARTUP

MAIN = "\n".join(
    (
        *coverage_by_area.HEADER,
        "| Registro do núcleo | 90 % | 100,0 % | 2 de 2 |",
        "| Plataforma | 80 % | 100,0 % | 3 de 3 |",
        "",
    )
)
DROP = "AVISO — Plataforma caiu de 100,0 % para 80,0 % sem cruzar o piso de 80 %"


class FloorsPreviousTests(fixture.FloorsFixture):
    def measure_and_compare(self) -> str:
        """Runs the step against the main's table and returns the job summary it wrote."""
        summary = self.root / "summary.md"
        os.environ[coverage_by_area.SUMMARY] = str(summary)
        support.write(self.root, PREVIOUS, MAIN)
        self.core((LOGGING, "L.cs", "1 1"), (STARTUP, "S.cs", "1 1 1 1 0"))

        result = self.run_step(self.root / PREVIOUS)

        self.assertTrue(result.ok)
        return summary.read_text(encoding="utf-8")

    def test_the_summary_lists_the_area_that_dropped_and_only_that_one(self) -> None:
        written = self.measure_and_compare()

        self.assertIn(f"\n- {DROP}\n", written)
        self.assertEqual(written.count("AVISO"), 1)

    def test_the_summary_says_which_table_the_drops_were_compared_with(self) -> None:
        written = self.measure_and_compare()

        self.assertIn(f"\n- quedas comparadas com {self.root / PREVIOUS}\n", written)

    def test_a_job_without_the_table_of_the_main_passes_and_the_summary_says_so(self) -> None:
        summary = self.root / "summary.md"
        os.environ[coverage_by_area.SUMMARY] = str(summary)
        self.core((LOGGING, "L.cs", "1 1"), (STARTUP, "S.cs", "1 1 1 1 0"))

        result = self.run_step()

        self.assertTrue(result.ok)
        self.assertIn(f"\n- {floors.NO_PREVIOUS}\n", summary.read_text(encoding="utf-8"))

    def test_under_actions_each_drop_is_also_a_warning_annotation(self) -> None:
        os.environ["GITHUB_ACTIONS"] = "true"
        printed = io.StringIO()

        with contextlib.redirect_stdout(printed):
            self.measure_and_compare()

        expected = "::warning title=Queda de cobertura::" + DROP.replace("%", "%25")
        self.assertEqual(printed.getvalue().splitlines(), [expected])

    def test_outside_actions_nothing_is_printed(self) -> None:
        os.environ.pop("GITHUB_ACTIONS", None)
        printed = io.StringIO()

        with contextlib.redirect_stdout(printed):
            self.measure_and_compare()

        self.assertEqual(printed.getvalue(), "")
