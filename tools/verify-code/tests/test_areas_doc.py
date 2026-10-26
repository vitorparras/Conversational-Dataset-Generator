"""Tests for the reader of the area table of docs/operacao/014: rows, floors, and refusals."""

from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from steps import areas_doc
from steps.areas_doc import Area

from . import support

LOGGING = "| Registro | `Core.Platform.Logging` | 90 % | sim | crítica |"
PLATFORM = "| Plataforma | `Core.Platform` | 80 % | não | demais pastas |"
CONTRACTS = "| Contrato | `Core.Boundary.Contracts` | isento | não | só tipos |"
SEPARATOR = "|---|---|---|---|---|"
#: The first table row of ``document`` sits on this line of the file.
FIRST_ROW = 6
REPOSITORY = support.VERIFY_CODE.parents[1]


def document(*rows: str, header: str = areas_doc.HEADER) -> str:
    """A document with a title, a paragraph, then the table with the given rows."""
    return "\n".join(("# 014", "", "Texto.", header, SEPARATOR, *rows, "", "Depois.")) + "\n"


class AreasDocTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)

    def read(self, text: str) -> areas_doc.AreaTable:
        support.write(self.root, areas_doc.DOCUMENT, text)
        return areas_doc.read_areas(self.root)

    def test_each_row_becomes_an_area_with_its_numeric_floor(self) -> None:
        table = self.read(document(LOGGING, PLATFORM))

        self.assertEqual(
            table.areas,
            (
                Area("Registro", "Core.Platform.Logging", 90, critical=True),
                Area("Plataforma", "Core.Platform", 80),
            ),
        )

    def test_the_critical_column_marks_the_area_whatever_its_floor(self) -> None:
        raised = LOGGING.replace("90 %", "95 %")
        lowered = PLATFORM.replace("não", "sim")

        table = self.read(document(raised, lowered))

        self.assertEqual([a.critical for a in table.areas], [True, True])

    def test_an_exempt_area_can_be_critical(self) -> None:
        table = self.read(document(CONTRACTS.replace("não", "sim")))

        self.assertEqual(table.areas, (Area("Contrato", "Core.Boundary.Contracts", None, True),))

    def test_a_critical_value_other_than_yes_or_no_fails_naming_the_line(self) -> None:
        row = LOGGING.replace("sim", "talvez")

        table = self.read(document(PLATFORM, row))

        self.assertEqual(table.areas, ())
        expected = f"{areas_doc.DOCUMENT}:{FIRST_ROW + 1}: Crítica 'talvez' não é `sim` nem `não`"
        self.assertEqual(table.problems, (expected,))

    def test_an_empty_critical_cell_fails_naming_the_line(self) -> None:
        row = LOGGING.replace("| sim |", "|  |")

        table = self.read(document(row))

        self.assertEqual(table.areas, ())
        self.assertIn(f"{areas_doc.DOCUMENT}:{FIRST_ROW}: Crítica ''", table.problems[0])

    def test_the_header_without_the_critical_column_is_not_the_fixed_header(self) -> None:
        header = "| Área | Projeto/pacote | Piso | Motivo |"

        table = self.read(document(LOGGING, header=header))

        self.assertIn("cabeçalho fixo", table.problems[0])

    def test_an_exempt_area_has_no_floor(self) -> None:
        table = self.read(document(CONTRACTS))

        self.assertEqual(table.areas, (Area("Contrato", "Core.Boundary.Contracts", None),))

    def test_a_floor_of_one_hundred_percent_is_read(self) -> None:
        table = self.read(document(LOGGING.replace("90 %", "100 %")))

        self.assertEqual(table.areas, (Area("Registro", "Core.Platform.Logging", 100, True),))

    def test_a_prefix_and_the_prefixes_under_it_coexist(self) -> None:
        table = self.read(document(PLATFORM, LOGGING))

        self.assertEqual(table.problems, ())

    def test_an_area_named_as_its_own_prefix_is_read(self) -> None:
        table = self.read(document("| tools | `tools` | 60 % | não | scripts |"))

        self.assertEqual(table.areas, (Area("tools", "tools", 60),))

    def test_an_area_named_as_another_rows_prefix_is_read(self) -> None:
        table = self.read(document(LOGGING, "| Core.Platform.Logging | `tools` | 60 % | não | x |"))

        self.assertEqual(table.problems, ())

    def test_a_document_that_reads_has_no_problems(self) -> None:
        table = self.read(document(LOGGING, CONTRACTS))

        self.assertEqual(table.problems, ())

    def test_the_repository_document_reads_every_area_without_problems(self) -> None:
        table = areas_doc.read_areas(REPOSITORY)

        self.assertEqual(table.problems, ())
        self.assertNotEqual(table.areas, ())

    def test_a_missing_document_fails(self) -> None:
        table = areas_doc.read_areas(self.root)

        self.assertEqual(
            table.problems, (f"{areas_doc.DOCUMENT} não existe: não há áreas para medir",)
        )

    def test_a_different_header_fails_and_no_area_is_read(self) -> None:
        header = "| Área | Pacote | Piso | Motivo |"

        table = self.read(document(LOGGING, header=header))

        self.assertEqual(table.areas, ())
        self.assertIn("cabeçalho fixo", table.problems[0])

    def test_a_table_without_separator_fails(self) -> None:
        text = document(LOGGING).replace(f"{SEPARATOR}\n", "")

        table = self.read(text)

        self.assertIn("falta a linha `|---|`", table.problems[0])

    def test_a_table_without_rows_fails(self) -> None:
        table = self.read(document())

        self.assertIn("não tem nenhuma linha", table.problems[0])

    def test_a_floor_without_percent_sign_fails_naming_the_line(self) -> None:
        row = LOGGING.replace("90 %", "90")

        table = self.read(document(PLATFORM, row))

        self.assertEqual(table.areas, ())
        self.assertIn(f"{areas_doc.DOCUMENT}:{FIRST_ROW + 1}: piso '90'", table.problems[0])

    def test_a_floor_above_one_hundred_fails_naming_the_line(self) -> None:
        row = LOGGING.replace("90 %", "190 %")

        table = self.read(document(row))

        self.assertIn(f"{areas_doc.DOCUMENT}:{FIRST_ROW}: piso '190 %'", table.problems[0])

    def test_a_second_non_numeric_floor_fails_naming_the_line(self) -> None:
        row = LOGGING.replace("90 %", "dispensado")

        table = self.read(document(CONTRACTS, row))

        self.assertEqual(table.areas, ())
        self.assertIn(f"{areas_doc.DOCUMENT}:{FIRST_ROW + 1}: piso 'dispensado'", table.problems[0])

    def test_every_malformed_line_is_named(self) -> None:
        first, second = LOGGING.replace("90 %", "90"), PLATFORM.replace("80 %", "n/a")

        table = self.read(document(first, CONTRACTS, second))

        self.assertEqual(len(table.problems), 2)
        self.assertIn(f":{FIRST_ROW + 2}:", table.problems[1])

    def test_a_row_with_a_missing_column_fails_naming_the_line(self) -> None:
        row = "| Registro | `Core.Platform.Logging` | 90 % |"

        table = self.read(document(row))

        self.assertIn(f"{areas_doc.DOCUMENT}:{FIRST_ROW}: a linha tem 3 colunas", table.problems[0])

    def test_a_prefix_without_backticks_fails_naming_the_line(self) -> None:
        row = LOGGING.replace("`Core.Platform.Logging`", "Core.Platform.Logging")

        table = self.read(document(row))

        self.assertIn(f"{areas_doc.DOCUMENT}:{FIRST_ROW}: área sem nome", table.problems[0])

    def test_a_duplicated_area_name_fails_naming_both_lines(self) -> None:
        row = PLATFORM.replace("Plataforma", "Registro")

        table = self.read(document(LOGGING, row))

        self.assertEqual(table.areas, ())
        self.assertEqual(
            table.problems,
            (f"{areas_doc.DOCUMENT}:{FIRST_ROW + 1}: `Registro` já aparece na linha {FIRST_ROW}",),
        )

    def test_a_header_with_an_extra_column_is_not_the_fixed_header(self) -> None:
        header = "| Área | Projeto/pacote | Piso | Crítica | Motivo | Dono |"

        table = self.read(document(LOGGING, header=header))

        self.assertIn("cabeçalho fixo", table.problems[0])

    def test_a_project_or_package_in_two_rows_fails_naming_both_lines(self) -> None:
        row = PLATFORM.replace("`Core.Platform`", "`Core.Platform.Logging`")

        table = self.read(document(LOGGING, row))

        self.assertEqual(table.areas, ())
        self.assertEqual(
            table.problems,
            (
                f"{areas_doc.DOCUMENT}:{FIRST_ROW + 1}: `Core.Platform.Logging` "
                f"já aparece na linha {FIRST_ROW}",
            ),
        )


class AreaOfTests(unittest.TestCase):
    LOGGING = Area("Registro", "Core.Platform.Logging", 90)
    PLATFORM = Area("Plataforma", "Core.Platform", 80)

    def test_the_longest_prefix_wins_whatever_the_row_order(self) -> None:
        area = areas_doc.area_of("Core.Platform.Logging.Formatter", (self.PLATFORM, self.LOGGING))

        self.assertEqual(area, self.LOGGING)

    def test_a_subfolder_without_row_falls_to_the_enclosing_prefix(self) -> None:
        area = areas_doc.area_of("Core.Platform.Clock.SystemClock", (self.LOGGING, self.PLATFORM))

        self.assertEqual(area, self.PLATFORM)

    def test_a_prefix_matches_whole_segments_only(self) -> None:
        area = areas_doc.area_of("Core.PlatformExtra.Thing", (self.LOGGING, self.PLATFORM))

        self.assertIsNone(area)

    def test_the_prefix_itself_is_owned(self) -> None:
        area = areas_doc.area_of("Core.Platform", (self.LOGGING, self.PLATFORM))

        self.assertEqual(area, self.PLATFORM)
