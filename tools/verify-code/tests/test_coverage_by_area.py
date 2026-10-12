"""Tests for steps/coverage_by_area.py: per-area coverage from Cobertura and coverage.py reports."""

import importlib.util
import unittest
from pathlib import Path

_spec = importlib.util.spec_from_file_location("verify_code_support", Path(__file__).resolve().parent / "_support.py")
support = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(support)

from steps import coverage_by_area  # noqa: E402
from steps.areas_doc import read_document  # noqa: E402
from steps.common import FLOORS_DOCUMENT, StepResult  # noqa: E402


class CoverageByAreaTests(support.RepositoryTestCase):
    def measure(self) -> dict[str, coverage_by_area.Measurement]:
        document = read_document(self.root / FLOORS_DOCUMENT)
        self.assertEqual(document.problems, [])
        entries = coverage_by_area.collect(self.context(), document, StepResult())
        measurements = coverage_by_area.aggregate(document.areas, entries, self.root)
        return {m.area: m for m in measurements}

    def test_reports_are_merged_line_by_line_across_files_and_classes(self) -> None:
        """Issue 38 CA-2: a line run by any report counts once, covered; a closure does not add lines."""
        logging = self.measure()["Platform.Logging"]
        self.assertEqual((logging.covered, logging.total), (3, 4))
        self.assertEqual(logging.percent, 75.0)

    def test_every_kind_of_area_is_read(self) -> None:
        measured = self.measure()
        self.assertEqual(measured["Platform.Configuration"].percent, 100.0)
        self.assertEqual(measured["language_service.auth"].percent, 50.0)
        self.assertEqual(measured["tools"].percent, 100.0)

    def test_an_area_nothing_reaches_is_listed_with_zero(self) -> None:
        """Issue 38 CA-3: zero, present, not missing - for code no test ran and for code not written yet."""
        measured = self.measure()
        host = measured["Host"]
        self.assertEqual((host.covered, host.total, host.percent), (0, 2, 0.0))
        self.assertIn("Boundary.Text", measured)
        self.assertEqual(measured["Boundary.Text"].percent, 0.0)
        self.assertEqual(measured["Boundary.Text"].status, coverage_by_area.ABSENT)

    def test_an_existing_area_with_no_measured_line_is_flagged(self) -> None:
        self.write("core/src/Core.Platform/Logging/Interfaces/IThing.cs", "// synthetic\n")
        self.edit_document(
            "| Host |", "| Platform.Interfaces | `Core.Platform.Logging.Interfaces` | 0 | Sintético |\n| Host |"
        )
        self.assertEqual(self.measure()["Platform.Interfaces"].status, coverage_by_area.UNMEASURED)

    def test_two_runs_over_the_same_reports_give_the_same_bytes(self) -> None:
        """Issue 38 CA-4: stability - the report has no timestamp and a fixed order."""
        first = coverage_by_area.run(self.context())
        first_json = (self.out_dir / coverage_by_area.REPORT_FILE).read_bytes()
        second = coverage_by_area.run(self.context())
        second_json = (self.out_dir / coverage_by_area.REPORT_FILE).read_bytes()
        self.assertTrue(first.ok and second.ok, first.lines + second.lines)
        self.assertEqual(first_json, second_json)
        self.assertEqual(first.lines, second.lines)

    def test_the_previous_report_is_kept_for_comparison(self) -> None:
        coverage_by_area.run(self.context())
        coverage_by_area.run(self.context())
        self.assertTrue((self.out_dir / coverage_by_area.PREVIOUS_FILE).is_file())

    def test_the_table_is_published_in_the_job_summary_when_there_is_one(self) -> None:
        """Issue 38 CA-5: the numbers are visible in the result, without downloading anything."""
        summary = self.root / "summary.md"
        result = coverage_by_area.run(self.context(environment={"GITHUB_STEP_SUMMARY": str(summary)}))
        self.assertTrue(result.ok, result.lines)
        text = summary.read_text(encoding="utf-8")
        self.assertIn("| Platform.Logging | Core.Platform.Logging | 3/4 | 75.00% |", text)
        self.assertIn("Boundary.Text", text)

    def test_the_table_is_always_printed_in_the_result(self) -> None:
        result = coverage_by_area.run(self.context())
        self.assertIn("| Platform.Logging | Core.Platform.Logging | 3/4 | 75.00% |", result.lines)

    def test_a_missing_report_fails_instead_of_measuring_nothing(self) -> None:
        (self.out_dir / "service" / "coverage.xml").unlink()
        result = coverage_by_area.run(self.context())
        self.assertFalse(result.ok)
        self.assertIn("serviço", " ".join(result.lines))

    def test_a_document_without_the_table_fails(self) -> None:
        self.edit_document("| Área | Projeto/pacote | Piso | Motivo |", "| Area | Projeto/pacote | Piso | Motivo |")
        result = coverage_by_area.run(self.context())
        self.assertFalse(result.ok)


if __name__ == "__main__":
    unittest.main()
