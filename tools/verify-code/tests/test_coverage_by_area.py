"""Tests for the coverage step: rows per area from the reports present, and where they publish."""

from __future__ import annotations

import os
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from steps import (
    areas_doc,
    coverage_by_area,
    test_core,
    test_real_service,
    test_service,
    test_tools,
)
from steps.common import StepResult

from . import support

DOCUMENT = "\n".join(
    (
        "# 014",
        support.TABLE_HEADER,
        "|---|---|---|---|---|",
        "| Registro do núcleo | `Core.Platform.Logging` | 90 % | sim | crítica |",
        "| Plataforma | `Core.Platform` | 80 % | não | demais pastas |",
        "| Contrato | `Core.Boundary.Contracts` | isento | não | só tipos |",
        "| Autenticação | `language_service.auth` | 90 % | sim | segredo |",
        "| Serviço | `language_service` | 80 % | não | demais módulos |",
        "| Ferramentas | `tools` | 60 % | não | scripts |",
        "",
    )
)
LOGGING = "Core.Platform.Logging.Formatter"
STARTUP = "Core.Platform.Startup.Runner"
CORE_REPORT = f"{test_core.RESULTS}/a.cobertura.xml"
SECOND_CORE_REPORT = f"{test_core.RESULTS}/b.cobertura.xml"
REAL_SERVICE_REPORT = f"{test_real_service.RESULTS}/c.cobertura.xml"
SERVICE_REPORT = f"{test_service.RESULTS}/{test_service.REPORT}"
TOOLS_REPORT = f"{test_tools.RESULTS}/{test_tools.REPORT}"


class CoverageByAreaTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)
        self.out = self.root / ".verify-code"
        support.write_areas(self.root, DOCUMENT)
        self.environment = mock.patch.dict(os.environ, clear=False)
        self.environment.start()
        self.addCleanup(self.environment.stop)
        os.environ.pop(coverage_by_area.SUMMARY, None)

    def report(self, relative: str, text: str) -> None:
        support.write(self.out, relative, text)

    def core(self, *classes: tuple[str, str, str]) -> None:
        self.report(CORE_REPORT, support.cobertura("Core.Platform", *classes))

    def run_step(self) -> StepResult:
        return coverage_by_area.run(support.context(self.root, support.FakeRunner()))

    def test_no_report_fails_instead_of_passing_empty(self) -> None:
        result = self.run_step()

        self.assertFalse(result.ok)
        self.assertIn("o passo não passa vazio", result.details[0])

    def test_a_malformed_document_fails_naming_the_line(self) -> None:
        support.write_areas(self.root, DOCUMENT.replace("60 %", "60"))
        self.core((LOGGING, "L.cs", "1"))

        result = self.run_step()

        self.assertFalse(result.ok)
        self.assertIn(f"{areas_doc.FOLDER}/tools.md: piso '60'", result.details[0])

    def test_an_unreadable_report_fails(self) -> None:
        self.report(CORE_REPORT, "<coverage><packages>")

        result = self.run_step()

        self.assertFalse(result.ok)
        self.assertIn("relatório de cobertura ilegível", result.details[0])

    def test_lines_count_in_the_area_with_the_longest_prefix(self) -> None:
        self.core((LOGGING, "L.cs", "1 0"), (STARTUP, "S.cs", "1 1 1"))

        result = self.run_step()

        self.assertIn("| Registro do núcleo | 90 % | 50,0 % | 1 de 2 |", result.details)
        self.assertIn("| Plataforma | 80 % | 100,0 % | 3 de 3 |", result.details)

    def test_an_area_no_line_reaches_appears_with_zero(self) -> None:
        self.core((LOGGING, "L.cs", "1"))

        result = self.run_step()

        self.assertIn("| Contrato | isento | 0,0 % | 0 de 0 |", result.details)

    def test_an_area_whose_lines_never_ran_appears_with_zero(self) -> None:
        self.core((LOGGING, "L.cs", "0 0 0"))

        result = self.run_step()

        self.assertIn("| Registro do núcleo | 90 % | 0,0 % | 0 de 3 |", result.details)

    def test_a_line_run_in_any_report_counts_as_covered_once(self) -> None:
        self.core((LOGGING, "L.cs", "0 1"))
        self.report(
            SECOND_CORE_REPORT, support.cobertura("Core.Platform", (LOGGING, "L.cs", "1 0"))
        )

        result = self.run_step()

        self.assertIn("| Registro do núcleo | 90 % | 100,0 % | 2 de 2 |", result.details)

    def test_a_line_only_the_real_service_tests_run_counts_as_covered(self) -> None:
        self.core((LOGGING, "L.cs", "0 1"))
        self.report(
            REAL_SERVICE_REPORT, support.cobertura("Core.Platform", (LOGGING, "L.cs", "1 0"))
        )

        result = self.run_step()

        self.assertIn("| Registro do núcleo | 90 % | 100,0 % | 2 de 2 |", result.details)

    def test_the_real_service_report_alone_still_measures_the_core(self) -> None:
        self.report(
            REAL_SERVICE_REPORT, support.cobertura("Core.Platform", (LOGGING, "L.cs", "1 0"))
        )

        result = self.run_step()

        self.assertIn("### Cobertura por área — núcleo (.NET)", result.details)

    def test_only_the_areas_of_the_measured_language_are_listed(self) -> None:
        self.core((LOGGING, "L.cs", "1"))

        result = self.run_step()

        self.assertNotIn("| Serviço | 80 % | 0,0 % | 0 de 0 |", result.details)
        self.assertEqual(result.details[0], "### Cobertura por área — núcleo (.NET)")

    def test_code_outside_every_area_is_listed_without_area(self) -> None:
        self.report(CORE_REPORT, support.cobertura("Other.Lib", ("Other.Lib.Thing", "T.cs", "1 0")))

        result = self.run_step()

        self.assertIn("| (sem área) | — | 50,0 % | 1 de 2 |", result.details)

    def test_the_service_report_fills_the_service_areas(self) -> None:
        source = self.root / "language-service"
        modules = (("src/language_service/auth.py", "1 1"), ("src/language_service/app.py", "1 0"))
        self.report(SERVICE_REPORT, support.coverage_py(source, *modules))

        result = self.run_step()

        self.assertIn("| Autenticação | 90 % | 100,0 % | 2 de 2 |", result.details)
        self.assertIn("| Serviço | 80 % | 50,0 % | 1 de 2 |", result.details)

    def test_the_tools_report_fills_the_tools_area(self) -> None:
        source = self.root / test_tools.TOOLS
        self.report(TOOLS_REPORT, support.coverage_py(source, ("verify-code/steps/a.py", "1 0 0")))

        result = self.run_step()

        self.assertIn("| Ferramentas | 60 % | 33,3 % | 1 de 3 |", result.details)

    def test_the_table_is_written_under_the_output_directory(self) -> None:
        self.core((LOGGING, "L.cs", "1"))

        result = self.run_step()

        written = (self.out / coverage_by_area.RESULTS / coverage_by_area.TABLE).read_text("utf-8")
        self.assertEqual(written, "\n".join(result.details[:-1]) + "\n")

    def test_the_table_is_appended_to_the_job_summary_ci_names(self) -> None:
        summary = self.root / "summary.md"
        summary.write_text("earlier\n", encoding="utf-8")
        os.environ[coverage_by_area.SUMMARY] = str(summary)
        self.core((LOGGING, "L.cs", "1"))

        result = self.run_step()

        expected = "earlier\n" + "\n".join(result.details[:-1]) + "\n"
        self.assertEqual(summary.read_text(encoding="utf-8"), expected)

    def test_two_runs_over_the_same_reports_publish_the_same_table(self) -> None:
        self.core((LOGGING, "L.cs", "1 0 1"), (STARTUP, "S.cs", "0 1"))
        first = self.run_step()

        second = self.run_step()

        self.assertEqual(first.details, second.details)
