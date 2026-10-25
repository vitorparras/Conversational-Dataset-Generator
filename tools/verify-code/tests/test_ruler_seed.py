"""Tests for the defects the ruler proof plants: each one is valid Python once planted."""

from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from steps import ruler_seed

from . import support

MARKER = ruler_seed.MARKER
AUTH = "def accepts(self):\n    return hmac.compare_digest(self.digest, other)\n"


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
