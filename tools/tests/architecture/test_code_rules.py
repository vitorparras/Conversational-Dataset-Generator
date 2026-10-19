"""Architecture test: the size limits of the code rules hold for the Python in tools/.

The measuring itself is proved against synthetic sources that break each limit, so a broken
measurement cannot make the real scan pass by accident.
"""

from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from .code_limits import Violation, scan, violations

TOOLS = Path(__file__).resolve().parents[2]


def rules_of(source: str) -> list[str]:
    return [violation.rule for violation in violations("sample.py", source)]


class CodeRulesTests(unittest.TestCase):
    def test_production_python_of_tools_respects_the_limits(self) -> None:
        self.assertEqual(scan(TOOLS), [])

    def test_a_file_over_250_lines_is_reported(self) -> None:
        source = "x = 1\n" * 251

        self.assertEqual(rules_of(source), ["file-lines"])

    def test_a_file_of_250_lines_passes(self) -> None:
        source = "x = 1\n" * 250

        self.assertEqual(rules_of(source), [])

    def test_a_function_over_40_lines_is_reported(self) -> None:
        source = "def work() -> None:\n" + "    pass\n" * 40

        self.assertEqual(rules_of(source), ["function-lines"])

    def test_a_function_of_40_lines_passes(self) -> None:
        source = "def work() -> None:\n" + "    pass\n" * 39

        self.assertEqual(rules_of(source), [])

    def test_a_method_with_five_parameters_besides_self_is_reported(self) -> None:
        source = "class A:\n    def work(self, a, b, c, d, e): ...\n"

        self.assertEqual(rules_of(source), ["parameters"])

    def test_a_method_with_four_parameters_besides_self_passes(self) -> None:
        source = "class A:\n    def work(self, a, b, c, d): ...\n"

        self.assertEqual(rules_of(source), [])

    def test_variadic_parameters_count(self) -> None:
        source = "def work(a, b, c, *rest, **options): ...\n"

        self.assertEqual(rules_of(source), ["parameters"])

    def test_three_levels_of_nesting_are_reported(self) -> None:
        source = (
            "def work(a):\n    for x in a:\n        for y in x:\n"
            "            if y:\n                pass\n"
        )

        self.assertEqual(rules_of(source), ["nesting"])

    def test_two_levels_of_nesting_pass(self) -> None:
        source = "def work(a):\n    for x in a:\n        if x:\n            pass\n"

        self.assertEqual(rules_of(source), [])

    def test_an_elif_chain_does_not_add_levels(self) -> None:
        source = (
            "def work(a):\n    for x in a:\n        if x == 1:\n            pass\n"
            "        elif x == 2:\n            pass\n        elif x == 3:\n            pass\n"
        )

        self.assertEqual(rules_of(source), [])

    def test_nesting_inside_try_and_with_counts(self) -> None:
        source = (
            "def work(a):\n    try:\n        with a:\n            if a:\n                pass\n"
            "    except OSError:\n        pass\n"
        )

        self.assertEqual(rules_of(source), ["nesting"])

    def test_nesting_through_while_is_reported(self) -> None:
        source = (
            "def work(a):\n    while a:\n        for x in a:\n"
            "            if x:\n                pass\n"
        )

        self.assertEqual(rules_of(source), ["nesting"])

    def test_nesting_through_match_case_is_reported(self) -> None:
        source = (
            "def work(a):\n    match a:\n        case 1:\n            for x in a:\n"
            "                if x:\n                    pass\n"
        )

        self.assertEqual(rules_of(source), ["nesting"])

    def test_nesting_through_except_is_reported(self) -> None:
        source = (
            "def work(a):\n    try:\n        pass\n    except OSError:\n"
            "        for x in a:\n            if x:\n                pass\n"
        )

        self.assertEqual(rules_of(source), ["nesting"])

    def test_nesting_through_finally_is_reported(self) -> None:
        source = (
            "def work(a):\n    try:\n        pass\n    finally:\n"
            "        for x in a:\n            if x:\n                pass\n"
        )

        self.assertEqual(rules_of(source), ["nesting"])

    def test_nesting_through_else_of_if_is_reported(self) -> None:
        source = (
            "def work(a):\n    if a:\n        pass\n    else:\n"
            "        for x in a:\n            if x:\n                pass\n"
        )

        self.assertEqual(rules_of(source), ["nesting"])

    def test_nesting_through_else_of_for_is_reported(self) -> None:
        source = (
            "def work(a):\n    for x in a:\n        pass\n    else:\n"
            "        if a:\n            if x:\n                pass\n"
        )

        self.assertEqual(rules_of(source), ["nesting"])

    def test_nesting_goes_through_a_nested_function(self) -> None:
        source = (
            "def work(a):\n    for x in a:\n        def inner():\n            for y in a:\n"
            "                if y:\n                    pass\n"
        )

        self.assertEqual(rules_of(source), ["nesting"])

    def test_a_nested_function_adds_no_level_of_its_own(self) -> None:
        source = (
            "def work(a):\n    for x in a:\n        def inner():\n"
            "            if x:\n                pass\n"
        )

        self.assertEqual(rules_of(source), [])

    def test_keyword_only_parameters_count(self) -> None:
        source = "def work(a, b, *, c, d, e): ...\n"

        self.assertEqual(rules_of(source), ["parameters"])

    def test_positional_only_parameters_count(self) -> None:
        source = "def work(a, b, c, /, d, e): ...\n"

        self.assertEqual(rules_of(source), ["parameters"])

    def test_an_async_function_is_measured(self) -> None:
        source = "async def work(a, b, c, d, e): ...\n"

        self.assertEqual(rules_of(source), ["parameters"])

    def test_scan_reports_a_production_violation_with_path_and_rule(self) -> None:
        with tempfile.TemporaryDirectory() as folder:
            tools = Path(folder)
            (tools / "pkg").mkdir()
            source = "def work(a, b, c, d, e): ...\n"
            (tools / "pkg" / "prod.py").write_text(source, encoding="utf-8")

            found = scan(tools)

        self.assertEqual(found, [Violation("pkg/prod.py", 1, "parameters", "work: 5 > 4")])

    def test_scan_ignores_the_same_violation_inside_tests(self) -> None:
        with tempfile.TemporaryDirectory() as folder:
            tools = Path(folder)
            (tools / "pkg" / "tests").mkdir(parents=True)
            source = "def work(a, b, c, d, e): ...\n"
            (tools / "pkg" / "tests" / "test_x.py").write_text(source, encoding="utf-8")

            found = scan(tools)

        self.assertEqual(found, [])


if __name__ == "__main__":
    unittest.main()
