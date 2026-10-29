"""Tests for the defects the ruler proof plants: each one is valid Python once planted."""

from __future__ import annotations

import ast
import tempfile
import unittest
from pathlib import Path

from steps import areas_doc, ruler_seed

from . import floors_fixture, support

MARKER = ruler_seed.MARKER
AUTH = "def accepts(self):\n    return hmac.compare_digest(self.digest, other)\n"
FIXTURE_ROW = "| Autenticação | `language_service.auth` | 90 % | sim | segredo |"
AUTH_ROW = FIXTURE_ROW.replace("Autenticação", ruler_seed.CRITICAL_AREA)
EXEMPT_ROW = AUTH_ROW.replace("90 %", "isento")
NOT_CRITICAL_ROW = AUTH_ROW.replace("sim", "não")
DOCUMENT = floors_fixture.DOCUMENT.replace(FIXTURE_ROW, AUTH_ROW)
AUTH_MODULE = "import hmac\n\n\ndef accepts(a, b):\n    return hmac.compare_digest(a, b)\n"
IMPORTS_AUTH = "from language_service.auth import SecretVerifier\n"
IMPORTS_APP = "from language_service.app import build\n"


class SeedsTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.copy = Path(self._tmp.name)

    def test_the_defect_flips_the_comparison_and_leaves_valid_python(self) -> None:
        support.write(self.copy, ruler_seed.DEFECT_FILE, AUTH)

        problem = ruler_seed.plant_defect(self.copy)

        planted = (self.copy / ruler_seed.DEFECT_FILE).read_text(encoding="utf-8")
        self.assertIsNone(problem)
        compile(planted, "auth.py", "exec")
        self.assertIn("return not hmac.compare_digest(", planted)

    def test_a_module_without_the_comparison_is_not_planted(self) -> None:
        support.write(self.copy, ruler_seed.DEFECT_FILE, "pass\n")

        self.assertIsNotNone(ruler_seed.plant_defect(self.copy))

    def test_the_false_property_is_valid_python_and_carries_the_marker(self) -> None:
        compile(ruler_seed.FALSE_PROPERTY, "seed.py", "exec")

        self.assertIn(f"def test_{MARKER}", ruler_seed.FALSE_PROPERTY)


class FloorAndMutationSeedsTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.copy = Path(self._tmp.name)
        support.write(self.copy, areas_doc.DOCUMENT, DOCUMENT)
        support.write(self.copy, ruler_seed.DEFECT_FILE, AUTH_MODULE)
        support.write(self.copy, "language-service/tests/unit/test_auth.py", IMPORTS_AUTH)
        support.write(self.copy, "language-service/tests/unit/test_app.py", IMPORTS_APP)

    def read(self, relative: str) -> str:
        return (self.copy / relative).read_text(encoding="utf-8")

    def test_the_uncovered_function_is_appended_to_the_module_and_leaves_valid_python(self) -> None:
        problem = ruler_seed.plant_uncovered(self.copy)

        planted = self.read(ruler_seed.DEFECT_FILE)
        self.assertIsNone(problem)
        compile(planted, "auth.py", "exec")
        self.assertTrue(planted.startswith(AUTH_MODULE))
        self.assertIn(f"def {MARKER}() -> int:", planted)

    def test_the_uncovered_function_adds_no_comparison_and_no_branch(self) -> None:
        tree = ast.parse(ruler_seed.UNCOVERED_FUNCTION)

        self.assertFalse(any(isinstance(n, ast.Compare | ast.If) for n in ast.walk(tree)))

    def test_a_module_that_is_no_longer_the_area_the_seed_names_is_not_planted(self) -> None:
        renamed = DOCUMENT.replace(ruler_seed.CRITICAL_AREA, "Outra")
        support.write(self.copy, areas_doc.DOCUMENT, renamed)

        self.assertIsNotNone(ruler_seed.plant_uncovered(self.copy))
        self.assertIsNotNone(ruler_seed.plant_weak_tests(self.copy))

    def test_a_module_without_a_floor_or_no_longer_critical_is_not_planted(self) -> None:
        for row in (EXEMPT_ROW, NOT_CRITICAL_ROW):
            with self.subTest(row):
                support.write(self.copy, areas_doc.DOCUMENT, DOCUMENT.replace(AUTH_ROW, row))

                self.assertIsNotNone(ruler_seed.plant_uncovered(self.copy))

    def test_a_copy_without_the_module_is_not_planted(self) -> None:
        (self.copy / ruler_seed.DEFECT_FILE).unlink()

        self.assertIsNotNone(ruler_seed.plant_uncovered(self.copy))

    def test_the_weak_tests_replace_the_tests_that_import_the_module_and_only_them(self) -> None:
        problem = ruler_seed.plant_weak_tests(self.copy)

        self.assertIsNone(problem)
        self.assertEqual(
            self.read("language-service/tests/unit/test_auth.py"), ruler_seed.WEAK_TEST
        )
        self.assertEqual(self.read("language-service/tests/unit/test_app.py"), IMPORTS_APP)

    def test_the_weak_test_is_valid_python_that_still_imports_the_module(self) -> None:
        tree = ast.parse(ruler_seed.WEAK_TEST)

        self.assertFalse(any(isinstance(n, ast.Assert | ast.Raise) for n in ast.walk(tree)))
        self.assertIn(
            ruler_seed.CRITICAL_MODULE, ruler_seed.mutation_service.imported(ruler_seed.WEAK_TEST)
        )

    def test_the_weak_tests_stay_selected_for_the_module_after_planting(self) -> None:
        ruler_seed.plant_weak_tests(self.copy)

        names = ruler_seed.weak_test_files(self.copy)

        self.assertEqual(names, ("language-service/tests/unit/test_auth.py",))

    def test_a_module_no_test_imports_is_not_planted(self) -> None:
        (self.copy / "language-service/tests/unit/test_auth.py").unlink()

        self.assertIsNotNone(ruler_seed.plant_weak_tests(self.copy))
        self.assertEqual(ruler_seed.weak_test_files(self.copy), ())

    def test_every_seed_names_the_files_it_may_change(self) -> None:
        touched = {seed.case: seed.touches(self.copy) for seed in ruler_seed.SEEDS}

        self.assertEqual(touched["cobertura abaixo do piso"], (ruler_seed.DEFECT_FILE,))
        self.assertEqual(
            touched["mutantes sobreviventes"], ("language-service/tests/unit/test_auth.py",)
        )

    def test_only_the_mutation_seed_waits_for_linux_and_names_a_module(self) -> None:
        by_case = {seed.case: seed for seed in ruler_seed.SEEDS}

        self.assertEqual(
            {case: (s.platform, s.module) for case, s in by_case.items() if s.platform or s.module},
            {"mutantes sobreviventes": ("linux", ruler_seed.CRITICAL_MODULE)},
        )

    def test_the_recognisers_demand_the_reason_the_defect_gives(self) -> None:
        below = f"{ruler_seed.CRITICAL_AREA}: medido 50,0 %, abaixo do piso de 90 %"
        module = f"{ruler_seed.CRITICAL_AREA} (`{ruler_seed.CRITICAL_MODULE}`)"
        ceiling = f"{module}: 100,0 % de sobreviventes passa do limite de 20 %"

        self.assertTrue(ruler_seed.names_the_area_below_floor(below))
        self.assertFalse(ruler_seed.names_the_area_below_floor("Serviço: abaixo do piso de 80 %"))
        self.assertFalse(ruler_seed.names_the_area_below_floor("falha de comando"))
        self.assertTrue(ruler_seed.names_the_survivors_over_ceiling(ceiling))
        self.assertFalse(
            ruler_seed.names_the_survivors_over_ceiling("`other`: de sobreviventes passa do limite")
        )
        self.assertFalse(
            ruler_seed.names_the_survivors_over_ceiling(
                f"`{ruler_seed.CRITICAL_MODULE}`: 3 mutantes não verificados"
            )
        )
