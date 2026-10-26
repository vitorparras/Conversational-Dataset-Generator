"""Tests for the suite that counts in the mutation of a service module: only the test files that
import the module, one mutmut run per module, and every file in one area only (decisão da #314)."""

from __future__ import annotations

from steps import areas_doc, mutation_service
from steps.areas_doc import Area

from . import mutation_fixture as fixture
from . import support
from .mutation_samples import DOCUMENT, meta
from .test_mutation import AUTH_META, KILLED_AUTH, healthy

AUTH = Area("Autenticação", "language_service.auth", 90, critical=True)
LOGGING = Area("Registro do serviço", "language_service.logging_", 90, critical=True)
INNER = Area("Interno", "language_service.logging_.inner", 80)
SERVICE = Area("Serviço", "language_service", 80)
AREAS = (AUTH, LOGGING, INNER, SERVICE)
TESTS = "language-service/tests"
INNER_ROW = "| Interno | `language_service.logging_.inner` | 80 % | não | ZQX |\n"
CRITICAL_INNER_ROW = INNER_ROW.replace("80 % | não", "80 % | sim")
INNER_META = "src/language_service/logging_/inner.py"
INNER_MUTANT = "language_service.logging_.inner.x_f__mutmut_1"
LOGGING_META = "src/language_service/logging_/__init__.py"
LOGGING_MUTANT = "language_service.logging_.x_install__mutmut_1"


class SelectionTests(fixture.MutationFixture):
    def select(self, area: Area) -> tuple[str, ...]:
        return mutation_service.test_files(self.root, area, AREAS)

    def write_test(self, name: str, source: str) -> None:
        support.write(self.root, f"{TESTS}/{name}", source)

    def test_a_plain_import_of_the_module_selects_the_file(self) -> None:
        self.write_test("unit/test_plain.py", "import language_service.auth\n")

        self.assertIn("tests/unit/test_plain.py", self.select(AUTH))

    def test_a_from_import_of_the_module_selects_the_file(self) -> None:
        self.write_test("unit/test_from.py", "from language_service.auth import Verifier\n")

        self.assertIn("tests/unit/test_from.py", self.select(AUTH))

    def test_a_from_import_of_the_module_by_name_selects_the_file(self) -> None:
        self.write_test("unit/test_name.py", "from language_service import auth\n")

        self.assertIn("tests/unit/test_name.py", self.select(AUTH))

    def test_an_import_of_a_submodule_of_the_package_selects_the_file(self) -> None:
        self.write_test("unit/test_sub.py", "import language_service.logging_.sink\n")

        self.assertIn("tests/unit/test_sub.py", self.select(LOGGING))

    def test_a_file_that_imports_another_module_is_not_selected(self) -> None:
        self.write_test("unit/test_other.py", "from language_service import logging_\n")

        self.assertNotIn("tests/unit/test_other.py", self.select(AUTH))

    def test_a_file_that_imports_only_the_package_root_is_not_selected(self) -> None:
        self.write_test("unit/test_root.py", "import language_service\n")

        self.assertEqual(self.select(AUTH), ("tests/unit/test_auth.py",))

    def test_a_file_that_imports_a_nested_area_is_selected_for_it_alone(self) -> None:
        self.write_test("unit/test_inner.py", "import language_service.logging_.inner\n")

        self.assertNotIn("tests/unit/test_inner.py", self.select(LOGGING))
        self.assertEqual(self.select(INNER), ("tests/unit/test_inner.py",))

    def test_a_file_that_reads_the_source_text_is_never_selected(self) -> None:
        self.write_test("architecture/test_reads.py", "import language_service.auth\n")

        self.assertEqual(self.select(AUTH), ("tests/unit/test_auth.py",))

    def test_the_suffix_form_of_a_test_file_name_is_selected(self) -> None:
        self.write_test("unit/auth_test.py", "import language_service.auth\n")

        self.assertIn("tests/unit/auth_test.py", self.select(AUTH))

    def test_a_file_that_is_not_a_test_name_is_not_selected(self) -> None:
        self.write_test("support/helpers.py", "import language_service.auth\n")

        self.assertEqual(self.select(AUTH), ("tests/unit/test_auth.py",))

    def test_a_file_that_does_not_parse_selects_nothing(self) -> None:
        self.write_test("unit/test_broken.py", "import language_service.auth (\n")

        self.assertEqual(self.select(AUTH), ("tests/unit/test_auth.py",))

    def test_a_relative_import_does_not_select_the_file(self) -> None:
        self.write_test("unit/test_relative.py", "from ..src import auth\n")

        self.assertEqual(self.select(AUTH), ("tests/unit/test_auth.py",))


class ModuleRunTests(fixture.MutationFixture):
    def replace_in_document(self, old: str, new: str) -> None:
        support.write(self.root, areas_doc.DOCUMENT, DOCUMENT.replace(old, new))

    def test_mutmut_runs_each_module_with_the_tests_of_that_module_only(self) -> None:
        runner = healthy()

        self.run_step(runner)

        folders = [
            f.name
            for c, f in zip(runner.commands, runner.folders, strict=True)
            if "--max-children" in c
        ]
        self.assertEqual(folders, ["language_service.auth", "language_service.logging_"])

    def test_a_module_no_test_imports_fails_without_running_mutmut_for_it(self) -> None:
        support.write(self.root, f"{TESTS}/unit/test_auth.py", "import language_service\n")
        runner = healthy()

        result = self.run_step(runner)

        self.assertFalse(result.ok)
        expected = "Autenticação (`language_service.auth`): nenhum teste de tests/ importa"
        self.assertIn(f"{expected} language_service.auth", result.details)
        self.assertEqual(len(runner.called("--max-children")), 1)

    def test_a_nested_area_is_left_out_of_the_module_that_contains_it(self) -> None:
        self.replace_in_document("| Serviço |", f"{INNER_ROW}| Serviço |")
        support.write(self.root, "language-service/src/language_service/logging_/inner.py", "")

        self.run_step(healthy())

        config = self.root / ".verify-code/mutation/service/language_service.logging_/setup.cfg"
        expected = "do_not_mutate =\n    src/language_service/logging_/inner.py\n"
        self.assertIn(expected, config.read_text(encoding="utf-8"))

    def test_a_nested_critical_area_is_counted_once_and_by_its_own_tests(self) -> None:
        self.replace_in_document("| Serviço |", f"{CRITICAL_INNER_ROW}| Serviço |")
        support.write(self.root, "language-service/src/language_service/logging_/inner.py", "")
        support.write(
            self.root, f"{TESTS}/unit/test_inner.py", "import language_service.logging_.inner\n"
        )
        runner = healthy()
        runner.metas[INNER_META] = meta({INNER_MUTANT: 1})

        result = self.run_step(runner)

        counts = ", 1 mortos, 0 sobreviventes (0,0 %)"
        inner = "Interno (`language_service.logging_.inner`): 1 gerados"
        logging = "Registro do serviço (`language_service.logging_`): 1 gerados"
        self.assertIn(f"{inner}{counts}", result.details)
        self.assertIn(f"{logging}{counts}", result.details)
        selected = self.root / ".verify-code/mutation/service/language_service.logging_/setup.cfg"
        self.assertNotIn("test_inner.py", selected.read_text(encoding="utf-8"))

    def test_a_survivor_of_the_module_stays_with_the_module_beside_a_nested_area(self) -> None:
        self.replace_in_document("| Serviço |", f"{CRITICAL_INNER_ROW}| Serviço |")
        support.write(self.root, "language-service/src/language_service/logging_/inner.py", "")
        support.write(
            self.root, f"{TESTS}/unit/test_inner.py", "import language_service.logging_.inner\n"
        )
        runner = healthy()
        runner.metas[INNER_META] = meta({INNER_MUTANT: 1})
        runner.metas[AUTH_META] = meta({KILLED_AUTH: 1})
        runner.metas[LOGGING_META] = meta({LOGGING_MUTANT: 0})

        result = self.run_step(runner)

        own = "Registro do serviço (`language_service.logging_`)"
        self.assertIn(f"{own}: 1 gerados, 0 mortos, 1 sobreviventes (100,0 %)", result.details)
        inner = "Interno (`language_service.logging_.inner`)"
        self.assertIn(f"{inner}: 1 gerados, 1 mortos, 0 sobreviventes (0,0 %)", result.details)
