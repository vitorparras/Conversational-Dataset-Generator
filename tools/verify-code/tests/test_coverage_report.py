"""Tests for the readers of Cobertura and coverage.py XML reports: units, lines and hits."""

from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from steps.coverage_report import MeasuredLine, cobertura, coverage_py

from . import support

FORMATTER = "Core.Platform.Logging.Formatter"


class CoberturaTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.report = Path(self._tmp.name) / "a.cobertura.xml"

    def read(self, text: str) -> tuple[MeasuredLine, ...]:
        self.report.write_text(text, encoding="utf-8")
        return cobertura(self.report)

    def test_each_class_line_is_read_once_with_its_hit(self) -> None:
        text = support.cobertura("Core.Platform", (FORMATTER, "F.cs", "3 0"))

        lines = self.read(text)

        self.assertEqual(
            lines,
            (MeasuredLine(FORMATTER, "F.cs", 1, True), MeasuredLine(FORMATTER, "F.cs", 2, False)),
        )

    def test_a_class_outside_the_namespace_of_its_assembly_goes_under_the_assembly(self) -> None:
        text = support.cobertura("Core.Host", ("Program.&lt;Main&gt;d__0", "Program.cs", "1"))

        lines = self.read(text)

        self.assertEqual(lines[0].unit, "Core.Host.Program.<Main>d__0")

    def test_a_report_with_a_byte_order_mark_is_read(self) -> None:
        text = "﻿" + support.cobertura("Core.Platform", (FORMATTER, "F.cs", "1"))

        lines = self.read(text)

        self.assertEqual(len(lines), 1)


class CoveragePyTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)
        self.report = self.root / "coverage.xml"

    def read(self, source: Path, file: str) -> tuple[MeasuredLine, ...]:
        self.report.write_text(support.coverage_py(source, (file, "1 0")), encoding="utf-8")
        return coverage_py(self.report, self.root, "language_service")

    def test_the_module_is_named_from_the_package_folder_down(self) -> None:
        lines = self.read(
            self.root / "language-service", "src/language_service/logging_/install.py"
        )

        self.assertEqual(lines[0].unit, "language_service.logging_.install")

    def test_hits_are_read_per_line(self) -> None:
        lines = self.read(self.root / "language-service", "src/language_service/auth.py")

        self.assertEqual((lines[0].hit, lines[1].hit), (True, False))

    def test_a_file_outside_the_repository_has_no_unit(self) -> None:
        lines = self.read(Path(self._tmp.name).parent, "language_service/auth.py")

        self.assertEqual(lines[0].unit, "")

    def test_a_file_outside_the_package_folder_has_no_unit(self) -> None:
        lines = self.read(self.root / "language-service", "src/other/auth.py")

        self.assertEqual(lines[0].unit, "")
