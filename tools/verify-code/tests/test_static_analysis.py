"""Tests for the static analysis step: findings, dispensations, parts and parse errors."""

from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

from steps import static_analysis
from steps.common import CommandOutcome, StepResult

from . import support
from .static_analysis_support import (
    SemgrepRunner,
    context_of,
    partial,
    repository,
    result,
    scan_report,
)

SERVICE = "language-service/src/zqx/sample.py"
CORE = "core/src/Zqx/Sample.cs"
TOOLS = "tools/zqx/sample.py"
REASON = "  # nosemgrep: secret-literal-python a synthetic value the test needs\n"


class StaticAnalysisScanTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = repository(Path(self._tmp.name))

    def run_step(self, runner: SemgrepRunner) -> StepResult:
        return static_analysis.run(context_of(self.root, runner))

    def scan_with(self, *results: dict[str, object]) -> StepResult:
        return self.run_step(SemgrepRunner(report=scan_report(results)))

    def test_a_clean_scan_of_the_three_parts_passes(self) -> None:
        result_ = self.run_step(SemgrepRunner())

        self.assertTrue(result_.ok, result_.details)
        self.assertIn("3 arquivos de core, language-service, tools analisados", result_.details[0])

    def test_a_finding_fails_naming_the_file_the_line_and_the_rule(self) -> None:
        found = self.scan_with(result(SERVICE, 2))

        self.assertFalse(found.ok)
        self.assertIn(f"{SERVICE}:2: secret-literal-python", found.details)

    def test_a_finding_dispensed_on_the_line_above_with_rule_and_reason_passes(self) -> None:
        support.write(self.root, SERVICE, f"x = 1\n{REASON}y = 2\n")

        found = self.scan_with(result(SERVICE, 3))

        self.assertTrue(found.ok, found.details)
        self.assertIn("(1 dispensado(s) no ponto)", found.details[0])

    def test_a_finding_dispensed_on_its_own_line_passes(self) -> None:
        support.write(self.root, SERVICE, f"x = 1{REASON}")

        found = self.scan_with(result(SERVICE, 1))

        self.assertTrue(found.ok, found.details)

    def test_a_dispensation_without_a_reason_fails(self) -> None:
        support.write(self.root, SERVICE, "x = 1  # nosemgrep: secret-literal-python\n")

        found = self.scan_with(result(SERVICE, 1))

        self.assertFalse(found.ok)
        self.assertIn("não diz o motivo", "\n".join(found.details))

    def test_a_reason_of_two_words_is_not_enough(self) -> None:
        support.write(self.root, SERVICE, "x = 1  # nosemgrep: secret-literal-python only test\n")

        found = self.scan_with(result(SERVICE, 1))

        self.assertFalse(found.ok)

    def test_a_dispensation_without_a_rule_fails(self) -> None:
        support.write(self.root, SERVICE, "x = 1  # nosemgrep a synthetic value here\n")

        found = self.scan_with(result(SERVICE, 1))

        self.assertIn(
            f"{SERVICE}:1: a dispensa não nomeia a regra secret-literal-python", found.details
        )

    def test_a_dispensation_of_another_rule_does_not_cover_the_finding(self) -> None:
        line = "x = 1  # nosemgrep: sql-concatenation-python a synthetic value here\n"
        support.write(self.root, SERVICE, line)

        found = self.scan_with(result(SERVICE, 1))

        self.assertFalse(found.ok)

    def test_a_dispensation_two_lines_above_does_not_cover_the_finding(self) -> None:
        support.write(self.root, SERVICE, f"{REASON}x = 1\ny = 2\n")

        found = self.scan_with(result(SERVICE, 3))

        self.assertFalse(found.ok)

    def test_a_dispensation_at_the_end_of_the_line_above_does_not_cover_the_finding(self) -> None:
        support.write(self.root, SERVICE, f"x = 1{REASON}y = 2\n")

        found = self.scan_with(result(SERVICE, 2))

        self.assertIn(f"{SERVICE}:2: secret-literal-python", found.details)

    def test_a_finding_of_low_severity_still_fails(self) -> None:
        low: dict[str, object] = {**result(SERVICE, 2), "extra": {"severity": "INFO"}}

        found = self.scan_with(low)

        self.assertIn(f"{SERVICE}:2: secret-literal-python", found.details)

    def test_a_dispensation_in_core_src_fails_even_with_a_reason(self) -> None:
        line = "// nosemgrep: secret-literal-csharp a synthetic value here\n"
        support.write(self.root, CORE, line + "internal static class Sample;\n")

        found = self.scan_with(result(CORE, 2, "secret-literal-csharp"))

        self.assertFalse(found.ok)
        self.assertIn(f"{CORE}:2: dispensa proibida em core/src", "\n".join(found.details))

    def test_a_part_without_any_file_analyzed_fails(self) -> None:
        report = scan_report(scanned=(SERVICE, TOOLS))

        found = self.run_step(SemgrepRunner(report=report))

        self.assertFalse(found.ok)
        self.assertIn("parte core/ sem analisador: nenhum arquivo .cs analisado", found.details)

    def test_a_part_with_files_of_the_other_language_only_fails(self) -> None:
        support.write(self.root, "core/zqx.py", "x = 1\n")

        found = self.run_step(
            SemgrepRunner(report=scan_report(scanned=(SERVICE, TOOLS, "core/zqx.py")))
        )

        self.assertFalse(found.ok)

    def test_a_finding_in_a_file_that_is_not_utf8_fails_naming_the_file(self) -> None:
        (self.root / SERVICE).write_bytes(b"x = 1\ny = '\xff'\n")

        found = self.scan_with(result(SERVICE, 2))

        self.assertIn(f"{SERVICE}:2: secret-literal-python", found.details)

    def test_a_partial_parse_is_listed_with_its_lines_without_failing(self) -> None:
        report = scan_report(errors=[partial(SERVICE, 2, 2)])

        found = self.run_step(SemgrepRunner(report=report))

        self.assertTrue(found.ok, found.details)
        self.assertIn(f"trecho não analisado: {SERVICE}:2", found.details)

    def test_a_partial_parse_of_a_file_that_is_not_utf8_is_listed(self) -> None:
        (self.root / SERVICE).write_bytes(b"x = 1\ny = '\xff'\nz = 3\n")

        found = self.run_step(SemgrepRunner(report=scan_report(errors=[partial(SERVICE, 2, 2)])))

        self.assertIn(f"trecho não analisado: {SERVICE}:2", found.details)

    def test_a_file_semgrep_cannot_parse_at_all_fails(self) -> None:
        report = scan_report(errors=[partial(SERVICE, 1, 3)])

        found = self.run_step(SemgrepRunner(report=report))

        self.assertFalse(found.ok)
        self.assertIn(
            f"{SERVICE}: arquivo não analisado, o semgrep não consegue lê-lo", found.details
        )

    def test_any_other_semgrep_error_fails(self) -> None:
        report = scan_report(errors=[{"type": "Timeout", "path": SERVICE}])

        found = self.run_step(SemgrepRunner(report=report))

        self.assertFalse(found.ok)
        self.assertIn(f"erro do semgrep em {SERVICE}: Timeout", found.details)

    def test_a_scan_without_report_fails(self) -> None:
        found = self.run_step(SemgrepRunner(report=None))

        self.assertFalse(found.ok)
        self.assertIn(
            "o semgrep falhou sem relatório; confira se existem core, language-service, tools",
            found.details,
        )

    def test_a_report_without_results_fails(self) -> None:
        report = json.dumps({"errors": [], "paths": {"scanned": [CORE, SERVICE, TOOLS]}})

        found = self.run_step(SemgrepRunner(report=report))

        self.assertFalse(found.ok)

    def test_a_malformed_report_fails(self) -> None:
        found = self.run_step(SemgrepRunner(report="{"))

        self.assertFalse(found.ok)

    def test_a_scan_that_exits_with_failure_fails_even_with_a_report(self) -> None:
        found = self.run_step(SemgrepRunner(scan=CommandOutcome(False, "boom")))

        self.assertIn(
            "o semgrep falhou com saída de erro; confira se existem core, language-service, tools",
            found.details,
        )


if __name__ == "__main__":
    unittest.main()
