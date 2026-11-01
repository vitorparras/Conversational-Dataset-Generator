"""Tests for the floors step: each measured area against the floor the document declares."""

from __future__ import annotations

import os

from steps import areas_doc, coverage_by_area

from . import floors_fixture as fixture
from . import support
from .floors_fixture import DOCUMENT, LOGGING, SERVICE_REPORT, STARTUP


class FloorsTests(fixture.FloorsFixture):
    def test_no_report_fails_instead_of_passing_empty(self) -> None:
        result = self.run_step()

        self.assertFalse(result.ok)
        self.assertIn("o passo não passa vazio", result.details[0])

    def test_an_area_below_its_floor_fails_naming_area_floor_and_measured(self) -> None:
        self.core((LOGGING, "L.cs", "1 0"))

        result = self.run_step()

        self.assertFalse(result.ok)
        expected = "Registro do núcleo: medido 50,0 %, abaixo do piso de 90 % (1 de 2 linhas)"
        self.assertEqual(result.details[0], expected)

    def test_an_area_exactly_at_its_floor_passes(self) -> None:
        self.core((LOGGING, "L.cs", "1 1 1 1 1 1 1 1 1 0"))

        result = self.run_step()

        self.assertTrue(result.ok)
        self.assertIn("Registro do núcleo: 90,0 % atende ao piso de 90 %", result.details)

    def test_an_exempt_area_is_not_compared(self) -> None:
        self.core(("Core.Boundary.Contracts.Shape", "C.cs", "0 0"), package="Core.Boundary")

        result = self.run_step()

        self.assertTrue(result.ok)
        self.assertIn("Contrato: isento, nada se compara (medido 0,0 %)", result.details)

    def test_an_area_with_a_floor_and_no_code_yet_does_not_fail(self) -> None:
        self.core((LOGGING, "L.cs", "1"))

        result = self.run_step()

        self.assertTrue(result.ok)
        expected = "Texto: sem código (0 de 0); o piso de 90 % vale quando houver linha"
        self.assertIn(expected, result.details)

    def test_measured_code_no_area_owns_fails_naming_it(self) -> None:
        self.core(("Other.Lib.Thing", "T.cs", "1 1"), package="Other.Lib")

        result = self.run_step()

        self.assertFalse(result.ok)
        expected = f"(sem área): nenhuma área de {areas_doc.FOLDER} possui `Other.Lib.Thing`"
        self.assertEqual(result.details[0], expected)

    def test_a_share_just_under_the_floor_is_cut_not_rounded_up_to_it(self) -> None:
        self.core((LOGGING, "L.cs", "1 " * 8999 + "0 " * 1001))

        result = self.run_step()

        self.assertIn("medido 89,9 %, abaixo do piso de 90 %", result.details[0])

    def test_the_same_share_fails_under_the_floor_the_document_declares(self) -> None:
        self.core((STARTUP, "S.cs", "1 1 1 0"))

        result = self.run_step()

        self.assertFalse(result.ok)
        self.assertIn("abaixo do piso de 80 %", result.details[0])

    def test_lowering_the_floor_in_the_document_changes_the_verdict(self) -> None:
        document = DOCUMENT.replace("80 % | não | demais pastas", "70 % | não | demais pastas")
        support.write_areas(self.root, document)
        self.core((STARTUP, "S.cs", "1 1 1 0"))

        result = self.run_step()

        self.assertTrue(result.ok)
        self.assertIn("Plataforma: 75,0 % atende ao piso de 70 %", result.details)

    def test_the_service_report_is_judged_against_the_service_floors(self) -> None:
        source = self.root / "language-service"
        modules = (("src/language_service/auth.py", "1 0"),)
        support.write(
            self.root / ".verify-code", SERVICE_REPORT, support.coverage_py(source, *modules)
        )

        result = self.run_step()

        self.assertFalse(result.ok)
        self.assertIn("Autenticação: medido 50,0 %, abaixo do piso de 90 %", result.details[0])

    def test_the_verdict_is_appended_to_the_job_summary_ci_names(self) -> None:
        summary = self.root / "summary.md"
        summary.write_text("earlier\n", encoding="utf-8")
        os.environ[coverage_by_area.SUMMARY] = str(summary)
        self.core((LOGGING, "L.cs", "1 0"))

        self.run_step()

        written = summary.read_text(encoding="utf-8")
        self.assertTrue(written.startswith("earlier\n### Pisos de cobertura — núcleo (.NET)\n"))
        self.assertIn("\n- Registro do núcleo: medido 50,0 %, abaixo do piso de 90 %", written)
