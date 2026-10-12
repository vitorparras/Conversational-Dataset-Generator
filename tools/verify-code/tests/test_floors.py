"""Tests for steps/floors.py: the floor comes from the document, and everything missing fails closed."""

import importlib.util
import json
import unittest
from pathlib import Path

_spec = importlib.util.spec_from_file_location("verify_code_support", Path(__file__).resolve().parent / "_support.py")
support = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(support)

from steps import coverage_by_area, floors  # noqa: E402
from steps.areas_doc import read_document  # noqa: E402
from steps.common import FLOORS_DOCUMENT  # noqa: E402


class FloorsTests(support.RepositoryTestCase):
    def measure_then_judge(self, *, ci: bool = False):
        measured = coverage_by_area.run(self.context())
        self.assertTrue(measured.ok, measured.lines)
        return floors.run(self.context(ci=ci))

    def test_a_repository_that_meets_every_floor_passes(self) -> None:
        result = self.measure_then_judge()
        self.assertTrue(result.ok, result.lines)

    def test_an_area_below_its_floor_fails_naming_area_floor_and_measured(self) -> None:
        """Issue 39 CA-1: the three things, so the failure can be acted on without opening anything."""
        self.edit_document("| Platform.Logging | `Core.Platform.Logging` | 50 |", "| Platform.Logging | `Core.Platform.Logging` | 90 |")
        result = self.measure_then_judge()
        self.assertFalse(result.ok)
        failure = next(line for line in result.lines if line.startswith("FALHA"))
        self.assertIn("Platform.Logging", failure)
        self.assertIn("75", failure)
        self.assertIn("90", failure)

    def test_the_new_value_of_the_document_is_used_without_touching_any_other_file(self) -> None:
        """Issue 39 CA-3: raise the floor in the document and the verdict changes."""
        self.assertTrue(self.measure_then_judge().ok)
        self.edit_document("| Platform.Configuration | `Core.Platform.Configuration` | 80% |", "| Platform.Configuration | `Core.Platform.Configuration` | 100 |")
        self.assertTrue(floors.run(self.context()).ok)
        self.edit_document("| Platform.Logging | `Core.Platform.Logging` | 50 |", "| Platform.Logging | `Core.Platform.Logging` | 76 |")
        self.assertFalse(floors.run(self.context()).ok)

    def test_an_area_without_a_floor_fails_and_is_not_a_floor_of_zero(self) -> None:
        """Issue 39 CA-4."""
        self.edit_document("| Platform.Logging | `Core.Platform.Logging` | 50 |", "| Platform.Logging | `Core.Platform.Logging` |  |")
        result = self.measure_then_judge()
        self.assertFalse(result.ok)
        self.assertIn("Platform.Logging", " ".join(result.lines))

    def test_a_measured_area_that_the_document_does_not_list_fails(self) -> None:
        coverage_by_area.run(self.context())
        report = self.out_dir / coverage_by_area.REPORT_FILE
        data = json.loads(report.read_text(encoding="utf-8"))
        data["areas"].append({"area": "Unlisted.Area", "project": "x", "covered": 1, "total": 1, "percent": 100.0, "status": "measured"})
        report.write_text(json.dumps(data), encoding="utf-8")
        result = floors.run(self.context())
        self.assertFalse(result.ok)
        self.assertIn("Unlisted.Area", " ".join(result.lines))

    def test_a_drop_that_does_not_cross_the_floor_is_a_visible_warning_and_not_a_failure(self) -> None:
        """Issue 39 CA-2."""
        self.measure_then_judge()
        report = self.out_dir / coverage_by_area.REPORT_FILE
        previous = json.loads(report.read_text(encoding="utf-8"))
        for area in previous["areas"]:
            if area["area"] == "Platform.Logging":
                area["percent"] = 95.0
        (self.out_dir / coverage_by_area.PREVIOUS_FILE).write_text(json.dumps(previous), encoding="utf-8")
        result = floors.run(self.context())
        self.assertTrue(result.ok, result.lines)
        self.assertTrue(any("Platform.Logging" in w and "caiu" in w for w in result.warnings), result.warnings)

    def test_the_measured_number_is_recorded_for_the_next_comparison(self) -> None:
        """Issue 39 CA-5."""
        result = self.measure_then_judge()
        self.assertIn("| Platform.Logging | 50% | 75.00% | ok |", result.lines)
        self.assertTrue((self.out_dir / coverage_by_area.REPORT_FILE).is_file())

    def test_an_area_that_does_not_exist_yet_is_a_warning_and_not_a_failure(self) -> None:
        result = self.measure_then_judge()
        self.assertTrue(result.ok)
        self.assertTrue(any("Boundary.Text" in w for w in result.warnings), result.warnings)

    def test_an_existing_area_with_a_floor_and_no_measured_line_fails(self) -> None:
        self.write("core/src/Core.Platform/Logging/Interfaces/IThing.cs", "// synthetic\n")
        self.edit_document("| Host |", "| Platform.Interfaces | `Core.Platform.Logging.Interfaces` | 10 | Sintético |\n| Host |")
        result = self.measure_then_judge()
        self.assertFalse(result.ok)
        self.assertIn("Platform.Interfaces", " ".join(result.lines))

    def test_a_module_in_a_critical_folder_without_a_row_fails_naming_the_module(self) -> None:
        """Issue 36 CA-3."""
        self.write("core/src/Core.Platform/Crypto/Hasher.cs", "// synthetic\n")
        self.write("language-service/src/language_service/newmodule.py", "# synthetic\n")
        result = self.measure_then_judge()
        self.assertFalse(result.ok)
        text = " ".join(result.lines)
        self.assertIn("core/src/Core.Platform/Crypto", text)
        self.assertIn("language-service/src/language_service/newmodule", text)

    def test_entry_points_and_package_markers_are_not_modules(self) -> None:
        self.write("language-service/src/language_service/__init__.py", "")
        self.write("language-service/src/language_service/__main__.py", "# synthetic\n")
        self.write("core/src/Core.Platform/obj/Generated.cs", "// synthetic\n")
        self.assertTrue(self.measure_then_judge().ok)

    def test_a_missing_coverage_report_fails(self) -> None:
        result = floors.run(self.context())
        self.assertFalse(result.ok)
        self.assertIn("coverage_by_area", " ".join(result.lines))

    # --- documents that are wrong on purpose ------------------------------------------------------

    def judge_broken_document(self, old: str, new: str, expected: str) -> None:
        self.edit_document(old, new)
        result = floors.run(self.context())
        self.assertFalse(result.ok, result.lines)
        self.assertIn(expected, " ".join(result.lines))

    def test_wrong_header_fails(self) -> None:
        self.judge_broken_document("| Área | Projeto/pacote | Piso | Motivo |", "| Área | Projeto | Piso | Motivo |", "cabeçalho exato")

    def test_a_floor_that_is_not_a_number_fails(self) -> None:
        coverage_by_area.run(self.context())
        self.judge_broken_document("| `Core.Host` | 0 |", "| `Core.Host` | alto |", "não é um número")

    def test_a_floor_above_one_hundred_fails(self) -> None:
        coverage_by_area.run(self.context())
        self.judge_broken_document("| `Core.Host` | 0 |", "| `Core.Host` | 120 |", "não é um número")

    def test_a_repeated_area_fails(self) -> None:
        self.judge_broken_document("| Host | `Core.Host` | 0 | Cola sintética |", "| Host | `Core.Host` | 0 | Cola |\n| Host | `Core.Host` | 0 | Cola |", "repetida")

    def test_a_row_with_the_wrong_number_of_columns_fails(self) -> None:
        self.judge_broken_document("| Host | `Core.Host` | 0 | Cola sintética |", "| Host | `Core.Host` | 0 |", "colunas")

    def test_a_missing_critical_folders_table_fails(self) -> None:
        self.judge_broken_document("| Pasta crítica | Motivo |", "| Pasta | Motivo |", "cabeçalho exato")

    def test_a_missing_document_fails(self) -> None:
        (self.root / FLOORS_DOCUMENT).unlink()
        result = floors.run(self.context())
        self.assertFalse(result.ok)

    def test_the_document_is_the_only_source_of_numbers(self) -> None:
        """Issue 36 CA-2: no number of a floor appears in the code of the steps."""
        document = read_document(self.root / FLOORS_DOCUMENT)
        self.assertEqual(document.problems, [])
        source = (Path(floors.__file__)).read_text(encoding="utf-8")
        for row in document.areas:
            self.assertNotIn(row.area, source)

    def test_parse_floor(self) -> None:
        for text, expected in (("80", 80.0), ("80%", 80.0), ("92,5", 92.5), (" 7 ", 7.0), ("0", 0.0), ("100", 100.0)):
            self.assertEqual(floors.parse_floor(text), expected, text)
        for text in ("", "alto", "101", "-1", "8o", "80 %%"):
            self.assertIsNone(floors.parse_floor(text), text)


if __name__ == "__main__":
    unittest.main()
