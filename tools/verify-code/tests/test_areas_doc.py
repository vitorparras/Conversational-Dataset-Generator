"""Tests for the reader of the area files of docs/operacao/areas: fields, floors, and refusals."""

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
REPOSITORY = support.VERIFY_CODE.parents[1]
FOLDER = areas_doc.FOLDER


def file_of(prefix: str) -> str:
    return f"{FOLDER}/{prefix}.md"


FIELDS = {
    "prefix": "`Core.Platform.Logging`",
    "floor": "90 %",
    "critical": "sim",
    "reason": "crítica",
}


def area_file(name: str = "Registro", **changed: str) -> str:
    """The text of one area file with the four fixed fields, each replaceable by keyword."""
    field = {**FIELDS, **changed}
    return "\n".join(
        (
            f"# {name}",
            "",
            "> Operacional. 2026-11-01.",
            "",
            f"- Projeto/pacote: {field['prefix']}",
            f"- Piso: {field['floor']}",
            f"- Crítica: {field['critical']}",
            f"- Motivo: {field['reason']}",
            "",
        )
    )


class AreasDocTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)

    def read(self, *rows: str) -> areas_doc.AreaTable:
        support.write_areas(self.root, "\n".join(rows))
        return areas_doc.read_areas(self.root)

    def read_files(self, **files: str) -> areas_doc.AreaTable:
        """Reads a folder holding the given files, named as the keywords say (`_` for `.`)."""
        for name, text in files.items():
            support.write(self.root, f"{FOLDER}/{name.replace('__', '.')}.md", text)
        return areas_doc.read_areas(self.root)

    def test_each_file_becomes_an_area_with_its_numeric_floor(self) -> None:
        table = self.read(LOGGING, PLATFORM)

        self.assertEqual(
            table.areas,
            (
                Area("Registro", "Core.Platform.Logging", 90, critical=True),
                Area("Plataforma", "Core.Platform", 80),
            ),
        )

    def test_the_names_after_absorve_up_to_the_semicolon_are_what_the_area_absorbs(self) -> None:
        row = PLATFORM.replace("demais pastas", "Absorve: `Versions`, `Clock`; sem `Outra`")

        table = self.read(row)

        self.assertEqual(table.areas[0].absorbs, ("Versions", "Clock"))

    def test_a_reason_goes_on_in_the_lines_indented_by_two_spaces(self) -> None:
        reason = "Absorve: `Versions`," + chr(10) + "  `Clock`; sem material"
        text = area_file(reason=reason, prefix="`Core.Platform`")

        table = self.read_files(Core__Platform=text)

        self.assertEqual(table.problems, ())
        self.assertEqual(table.areas[0].absorbs, ("Versions", "Clock"))

    def test_a_line_that_is_not_indented_does_not_go_on_with_the_reason(self) -> None:
        text = area_file(reason="Absorve: `Versions`; x") + "`Clock`" + chr(10)

        table = self.read_files(Core__Platform__Logging=text)

        self.assertEqual(table.areas[0].absorbs, ("Versions",))

    def test_a_reason_without_absorve_absorbs_nothing(self) -> None:
        table = self.read(PLATFORM.replace("demais pastas", "usa `Versions`"))

        self.assertEqual(table.areas[0].absorbs, ())

    def test_the_critical_field_marks_the_area_whatever_its_floor(self) -> None:
        raised = LOGGING.replace("90 %", "95 %")
        lowered = PLATFORM.replace("não", "sim")

        table = self.read(raised, lowered)

        self.assertEqual([a.critical for a in table.areas], [True, True])

    def test_an_exempt_area_can_be_critical(self) -> None:
        table = self.read(CONTRACTS.replace("não", "sim"))

        self.assertEqual(table.areas, (Area("Contrato", "Core.Boundary.Contracts", None, True),))

    def test_a_critical_value_other_than_yes_or_no_fails_naming_the_file(self) -> None:
        table = self.read(PLATFORM, LOGGING.replace("sim", "talvez"))

        self.assertEqual(table.areas, ())
        expected = f"{file_of('Core.Platform.Logging')}: Crítica 'talvez' não é `sim` nem `não`"
        self.assertEqual(table.problems, (expected,))

    def test_an_empty_critical_field_fails_naming_the_file(self) -> None:
        table = self.read_files(Core__Platform__Logging=area_file(critical=""))

        self.assertEqual(table.areas, ())
        self.assertIn(f"{file_of('Core.Platform.Logging')}: Crítica ''", table.problems[0])

    def test_an_exempt_area_has_no_floor(self) -> None:
        table = self.read(CONTRACTS)

        self.assertEqual(table.areas, (Area("Contrato", "Core.Boundary.Contracts", None),))

    def test_a_floor_of_one_hundred_percent_is_read(self) -> None:
        table = self.read(LOGGING.replace("90 %", "100 %"))

        self.assertEqual(table.areas, (Area("Registro", "Core.Platform.Logging", 100, True),))

    def test_a_prefix_and_the_prefixes_under_it_coexist(self) -> None:
        table = self.read(PLATFORM, LOGGING)

        self.assertEqual(table.problems, ())

    def test_an_area_named_as_its_own_prefix_is_read(self) -> None:
        table = self.read("| tools | `tools` | 60 % | não | scripts |")

        self.assertEqual(table.areas, (Area("tools", "tools", 60),))

    def test_an_area_named_as_another_files_prefix_is_read(self) -> None:
        table = self.read(LOGGING, "| Core.Platform.Logging | `tools` | 60 % | não | x |")

        self.assertEqual(table.problems, ())

    def test_a_folder_that_reads_has_no_problems(self) -> None:
        table = self.read(LOGGING, CONTRACTS)

        self.assertEqual(table.problems, ())

    def test_the_repository_folder_reads_every_area_without_problems(self) -> None:
        table = areas_doc.read_areas(REPOSITORY)

        self.assertEqual(table.problems, ())
        self.assertNotEqual(table.areas, ())

    def test_a_missing_folder_fails(self) -> None:
        table = areas_doc.read_areas(self.root)

        self.assertEqual(table.problems, (f"{FOLDER} não existe: não há áreas para medir",))

    def test_an_empty_folder_fails(self) -> None:
        (self.root / FOLDER).mkdir(parents=True)

        table = areas_doc.read_areas(self.root)

        self.assertEqual(table.problems, (f"{FOLDER}: a pasta não tem nenhum arquivo de área",))

    def test_a_file_without_a_field_fails_naming_the_file_and_the_field(self) -> None:
        for field in areas_doc.FIELDS:
            with self.subTest(field):
                text = "\n".join(
                    line for line in area_file().splitlines() if not line.startswith(f"- {field}:")
                )

                table = self.read_files(Core__Platform__Logging=text)

                self.assertEqual(table.areas, ())
                missing = f"{file_of('Core.Platform.Logging')}: falta o campo `{field}`"
                self.assertIn(missing, table.problems)

    def test_a_field_that_appears_twice_fails_naming_the_line(self) -> None:
        text = area_file() + "- Piso: 80 %\n"

        table = self.read_files(Core__Platform__Logging=text)

        self.assertEqual(table.areas, ())
        expected = f"{file_of('Core.Platform.Logging')}:9: o campo `Piso` já aparece antes"
        self.assertEqual(table.problems, (expected,))

    def test_a_file_without_a_title_fails(self) -> None:
        text = area_file().replace("# Registro", "Registro")

        table = self.read_files(Core__Platform__Logging=text)

        self.assertEqual(table.areas, ())
        self.assertIn(f"{file_of('Core.Platform.Logging')}:1: falta o título", table.problems[0])

    def test_a_floor_without_percent_sign_fails_naming_the_file(self) -> None:
        table = self.read(PLATFORM, LOGGING.replace("90 %", "90"))

        self.assertEqual(table.areas, ())
        self.assertIn(f"{file_of('Core.Platform.Logging')}: piso '90'", table.problems[0])

    def test_a_floor_above_one_hundred_fails_naming_the_file(self) -> None:
        table = self.read(LOGGING.replace("90 %", "190 %"))

        self.assertIn(f"{file_of('Core.Platform.Logging')}: piso '190 %'", table.problems[0])

    def test_a_non_numeric_floor_other_than_exempt_fails_naming_the_file(self) -> None:
        table = self.read(CONTRACTS, LOGGING.replace("90 %", "dispensado"))

        self.assertEqual(table.areas, ())
        self.assertIn(f"{file_of('Core.Platform.Logging')}: piso 'dispensado'", table.problems[0])

    def test_every_malformed_file_is_named(self) -> None:
        first, second = LOGGING.replace("90 %", "90"), PLATFORM.replace("80 %", "n/a")

        table = self.read(first, CONTRACTS, second)

        self.assertEqual(len(table.problems), 2)
        self.assertIn(f"{file_of('Core.Platform')}:", table.problems[1])

    def test_a_prefix_without_backticks_fails_naming_the_file(self) -> None:
        text = area_file(prefix="Core.Platform.Logging")

        table = self.read_files(Core__Platform__Logging=text)

        self.assertIn(f"{file_of('Core.Platform.Logging')}: projeto/pacote", table.problems[0])

    def test_a_file_not_named_by_its_prefix_fails_naming_the_file(self) -> None:
        table = self.read_files(Outro=area_file())

        self.assertEqual(table.areas, ())
        self.assertEqual(
            table.problems, (f"{file_of('Outro')}: o arquivo se chama Core.Platform.Logging.md",)
        )

    def test_a_prefix_in_two_files_fails_naming_both(self) -> None:
        table = self.read_files(Core__Platform__Logging=area_file(), Outro=area_file(name="Outra"))

        self.assertEqual(table.areas, ())
        self.assertIn(
            f"{file_of('Outro')}: `Core.Platform.Logging` já aparece em "
            f"{file_of('Core.Platform.Logging')}",
            table.problems,
        )

    def test_a_duplicated_area_name_fails_naming_both_files(self) -> None:
        table = self.read(LOGGING, PLATFORM.replace("Plataforma", "Registro"))

        self.assertEqual(table.areas, ())
        self.assertEqual(
            table.problems,
            (
                f"{file_of('Core.Platform')}: `Registro` já aparece em "
                f"{file_of('Core.Platform.Logging')}",
            ),
        )

    def test_an_entry_that_is_not_an_area_file_fails_naming_it(self) -> None:
        support.write_areas(self.root, LOGGING)
        support.write(self.root, f"{FOLDER}/rascunho.txt", "x\n")
        (self.root / FOLDER / "pasta").mkdir()

        table = areas_doc.read_areas(self.root)

        self.assertEqual(table.areas, ())
        self.assertEqual(
            table.problems,
            (
                f"{FOLDER}/pasta: não é um arquivo `.md` de área",
                f"{FOLDER}/rascunho.txt: não é um arquivo `.md` de área",
            ),
        )


class AreaOfTests(unittest.TestCase):
    LOGGING = Area("Registro", "Core.Platform.Logging", 90)
    PLATFORM = Area("Plataforma", "Core.Platform", 80)

    def test_the_longest_prefix_wins_whatever_the_file_order(self) -> None:
        area = areas_doc.area_of("Core.Platform.Logging.Formatter", (self.PLATFORM, self.LOGGING))

        self.assertEqual(area, self.LOGGING)

    def test_a_subfolder_without_file_falls_to_the_enclosing_prefix(self) -> None:
        area = areas_doc.area_of("Core.Platform.Clock.SystemClock", (self.LOGGING, self.PLATFORM))

        self.assertEqual(area, self.PLATFORM)

    def test_a_prefix_matches_whole_segments_only(self) -> None:
        area = areas_doc.area_of("Core.PlatformExtra.Thing", (self.LOGGING, self.PLATFORM))

        self.assertIsNone(area)

    def test_the_prefix_itself_is_owned(self) -> None:
        area = areas_doc.area_of("Core.Platform", (self.LOGGING, self.PLATFORM))

        self.assertEqual(area, self.PLATFORM)
