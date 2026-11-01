"""Tests for the floors step: the project and package tree, and the comparison with --previous."""

from __future__ import annotations

from steps import floors

from . import floors_fixture as fixture
from . import support
from .floors_fixture import (
    HIGHER_BEFORE,
    LOGGING,
    LOWER_BEFORE,
    PREVIOUS,
    SAME_BEFORE,
    STARTUP,
    WITHOUT_SERVICE_ROOT,
)


class FloorsTreeAndPreviousTests(fixture.FloorsFixture):
    def test_a_core_project_no_area_owns_fails_naming_it(self) -> None:
        self.project("Core.Vault")
        self.core((LOGGING, "L.cs", "1"))

        result = self.run_step()

        self.assertFalse(result.ok)
        self.assertEqual(result.details[0].split(":")[0], "core/src/Core.Vault/")

    def test_a_service_package_no_area_owns_fails_naming_it(self) -> None:
        support.write_areas(self.root, WITHOUT_SERVICE_ROOT)
        self.package("vault")
        self.core((LOGGING, "L.cs", "1"))

        result = self.run_step()

        self.assertFalse(result.ok)
        folder = "language-service/src/language_service/vault/"
        self.assertEqual(result.details[0].split(":")[0], folder)

    def test_projects_and_packages_an_area_owns_pass(self) -> None:
        support.write_areas(self.root, WITHOUT_SERVICE_ROOT)
        self.project("Core.Platform")
        self.package("auth")
        self.core((LOGGING, "L.cs", "1"))

        result = self.run_step()

        self.assertTrue(result.ok)

    def test_folders_without_a_project_or_package_file_are_not_judged(self) -> None:
        support.write_areas(self.root, WITHOUT_SERVICE_ROOT)
        self.folder("core/src/Core.Vault/obj")
        self.folder("language-service/src/language_service/__pycache__")
        self.core((LOGGING, "L.cs", "1"))

        result = self.run_step()

        self.assertTrue(result.ok)

    def test_an_area_whose_project_exists_but_no_report_read_fails(self) -> None:
        self.project("Core.Boundary.Text")
        self.core((LOGGING, "L.cs", "1"))

        result = self.run_step()

        self.assertFalse(result.ok)
        expected = "Texto: código sem medição: nenhum relatório cobre `Core.Boundary.Text`"
        self.assertEqual(result.details[0], expected)

    def test_an_area_whose_project_is_read_through_its_sub_areas_is_without_code(self) -> None:
        self.project("Core.Platform")
        self.core((LOGGING, "L.cs", "1"))

        result = self.run_step()

        self.assertTrue(result.ok)
        expected = "Plataforma: sem código (0 de 0); o piso de 80 % vale quando houver linha"
        self.assertIn(expected, result.details)

    def test_a_drop_that_stays_above_the_floor_warns_without_failing(self) -> None:
        support.write(self.root, PREVIOUS, HIGHER_BEFORE)
        self.core((STARTUP, "S.cs", "1 1 1 1 0"))

        result = self.run_step(self.root / PREVIOUS)

        self.assertTrue(result.ok)
        expected = "AVISO — Plataforma caiu de 100,0 % para 80,0 % sem cruzar o piso de 80 %"
        self.assertIn(expected, result.details)

    def test_a_rise_against_the_previous_table_does_not_warn(self) -> None:
        support.write(self.root, PREVIOUS, LOWER_BEFORE)
        self.core((STARTUP, "S.cs", "1 1 1 1 0"))

        result = self.run_step(self.root / PREVIOUS)

        self.assertTrue(result.ok)
        self.assertNotIn("AVISO", "\n".join(result.details))

    def test_an_unchanged_share_against_the_previous_table_does_not_warn(self) -> None:
        support.write(self.root, PREVIOUS, SAME_BEFORE)
        self.core((STARTUP, "S.cs", "1 1 1 1 0"))

        result = self.run_step(self.root / PREVIOUS)

        self.assertTrue(result.ok)
        self.assertNotIn("AVISO", "\n".join(result.details))

    def test_a_missing_previous_file_fails_saying_so(self) -> None:
        self.core((LOGGING, "L.cs", "1"))

        result = self.run_step(self.root / PREVIOUS)

        self.assertFalse(result.ok)
        self.assertIn("arquivo não existe", result.details[0])

    def test_a_previous_file_without_table_rows_fails(self) -> None:
        support.write(self.root, PREVIOUS, "# outra coisa\n")
        self.core((LOGGING, "L.cs", "1"))

        result = self.run_step(self.root / PREVIOUS)

        self.assertFalse(result.ok)
        self.assertIn("nenhuma linha de tabela de cobertura", result.details[0])

    def test_without_previous_the_result_says_there_is_nothing_to_compare(self) -> None:
        self.core((LOGGING, "L.cs", "1"))

        result = self.run_step()

        self.assertIn(floors.NO_PREVIOUS, result.details)
