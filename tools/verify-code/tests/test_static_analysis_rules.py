"""Tests for the static analysis step before the scan: the pinned tool, the rules, the command."""

from __future__ import annotations

import re
import tempfile
import unittest
from pathlib import Path

from steps import static_analysis
from steps.common import CommandOutcome, StepResult

from . import support
from .static_analysis_support import SemgrepRunner, context_of, repository, rule_report
from .test_secrets_configuration import job_of

ROOT = support.VERIFY_CODE.parents[1]
WORKFLOW = ROOT / ".github" / "workflows" / "verify-code.yml"
RULE_FILE = f"{static_analysis.RULES}/secret.yml"


def rule_text() -> str:
    """Every rule file of the repository, joined."""
    folder = ROOT / static_analysis.RULES
    return "".join(path.read_text(encoding="utf-8") for path in sorted(folder.glob("*.yml")))


def failed_rule(rule: str) -> dict[str, object]:
    return {rule: {"passed": False}}


class StaticAnalysisToolTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = repository(Path(self._tmp.name))

    def run_step(self, runner: SemgrepRunner) -> StepResult:
        return static_analysis.run(context_of(self.root, runner))

    def test_a_missing_semgrep_fails_naming_the_requirements_to_install(self) -> None:
        runner = SemgrepRunner(version=CommandOutcome(False, "'semgrep' não está no PATH"))

        found = self.run_step(runner)

        self.assertFalse(found.ok)
        self.assertIn(f"pip install -r {static_analysis.REQUIREMENTS}", found.details[0])
        self.assertEqual(len(runner.commands), 1)

    def test_another_semgrep_version_fails(self) -> None:
        found = self.run_step(SemgrepRunner(version=CommandOutcome(True, "1.0.0\n")))

        self.assertFalse(found.ok)
        self.assertIn("semgrep 1.179.0 não encontrado (há: 1.0.0)", found.details[0])

    def test_requirements_without_the_pin_fail(self) -> None:
        support.write(self.root, static_analysis.REQUIREMENTS, "semgrep>=1\n")

        found = self.run_step(SemgrepRunner())

        self.assertIn("não fixa a versão do semgrep", found.details[0])

    def test_a_missing_rule_folder_fails_without_running_the_rule_tests(self) -> None:
        (self.root / RULE_FILE).unlink()
        runner = SemgrepRunner()

        found = self.run_step(runner)

        self.assertIn("conjunto de regras ausente", found.details[0])
        self.assertEqual(len(runner.commands), 1)

    def test_a_rule_narrowed_by_paths_fails(self) -> None:
        dump = "{ Rule.id = (zqx, _); paths = None }\n" * 3 + "{ Rule.id = (y, _); paths = Some }"

        found = self.run_step(SemgrepRunner(dump=CommandOutcome(True, dump)))

        self.assertIn("com `paths:` ou que o semgrep não carrega", found.details[0])

    def test_a_rule_semgrep_does_not_load_fails(self) -> None:
        dump = "{ Rule.id = (zqx, _); paths = None }\n" * 3

        found = self.run_step(SemgrepRunner(dump=CommandOutcome(True, dump)))

        self.assertIn("com `paths:` ou que o semgrep não carrega", found.details[0])

    def test_a_required_rule_absent_from_the_rule_tests_fails(self) -> None:
        rules = CommandOutcome(True, rule_report(passed=("secret-literal-python",)))

        found = self.run_step(SemgrepRunner(rules=rules))

        self.assertIn(
            f"regra sql-concatenation-csharp ausente ou desligada em {static_analysis.RULES}/",
            found.details,
        )

    def test_a_rule_that_misses_its_marked_examples_fails(self) -> None:
        report = rule_report(checks=failed_rule("secret-literal-csharp"))

        found = self.run_step(SemgrepRunner(rules=CommandOutcome(False, report)))

        self.assertIn(
            "a regra secret-literal-csharp não acusa só as linhas marcadas nos exemplos",
            found.details,
        )

    def test_a_rule_file_without_examples_fails(self) -> None:
        report = rule_report(config_missing_tests=[RULE_FILE])

        found = self.run_step(SemgrepRunner(rules=CommandOutcome(False, report)))

        self.assertIn(
            f"regra sem exemplos em {static_analysis.EXAMPLES}/: {RULE_FILE}", found.details
        )

    def test_a_rule_file_with_errors_fails(self) -> None:
        report = rule_report(config_with_errors=[RULE_FILE])

        found = self.run_step(SemgrepRunner(rules=CommandOutcome(False, report)))

        self.assertIn(f"arquivo de regras com erro: {RULE_FILE}", found.details)

    def test_rule_tests_without_output_fail(self) -> None:
        found = self.run_step(SemgrepRunner(rules=CommandOutcome(False, "")))

        self.assertIn(
            f"`semgrep --test --config {static_analysis.RULES} {static_analysis.EXAMPLES}` "
            "falhou sem relatório; rode-o para ver o motivo",
            found.details,
        )

    def test_the_rule_tests_read_the_rules_and_examples_of_the_repository(self) -> None:
        runner = SemgrepRunner()

        self.run_step(runner)

        self.assertEqual(
            runner.commands[2][:6],
            [
                "semgrep",
                "--test",
                "--json",
                "--config",
                static_analysis.RULES,
                static_analysis.EXAMPLES,
            ],
        )


class ScanCommandTests(unittest.TestCase):
    def setUp(self) -> None:
        self.command = static_analysis.scan_command(Path("report.json"))

    def test_the_scan_sends_no_metrics_and_checks_no_version(self) -> None:
        self.assertIn("--metrics=off", self.command)
        self.assertIn("--disable-version-check", self.command)

    def test_the_scan_skips_no_file_for_its_size(self) -> None:
        self.assertIn("--max-target-bytes=0", self.command)

    def test_the_rules_come_from_the_repository_folder(self) -> None:
        self.assertEqual(self.command[self.command.index("--config") + 1], static_analysis.RULES)

    def test_the_step_judges_dispensations_instead_of_semgrep(self) -> None:
        self.assertIn("--disable-nosem", self.command)

    def test_no_semgrepignore_skips_tests_and_only_the_rule_examples_are_excluded(self) -> None:
        self.assertIn("--x-ignore-semgrepignore-files", self.command)
        self.assertEqual(
            self.command[self.command.index("--exclude") + 1], static_analysis.EXAMPLES
        )
        self.assertEqual(self.command.count("--exclude"), 1)

    def test_the_scan_covers_the_core_the_service_and_the_tools(self) -> None:
        self.assertEqual(self.command[-3:], ["core", "language-service", "tools"])


class RepositoryRulesTests(unittest.TestCase):
    def test_the_requirements_pin_one_exact_semgrep_version(self) -> None:
        self.assertRegex(static_analysis.pinned_version(ROOT), r"^\d+\.\d+\.\d+$")

    def test_every_rule_of_the_repository_carries_the_repository_licence(self) -> None:
        text = rule_text()

        self.assertEqual(text.count("license: AGPL-3.0"), len(re.findall(r"^  - id: ", text, re.M)))

    def test_every_required_rule_is_declared_in_the_repository(self) -> None:
        text = rule_text()

        self.assertEqual(
            sorted(re.findall(r"^  - id: (\S+)$", text, re.M)),
            sorted(static_analysis.REQUIRED_RULES),
        )

    def test_the_workflow_job_installs_the_pinned_semgrep_and_runs_on_every_event(self) -> None:
        job = job_of(WORKFLOW.read_text(encoding="utf-8"), "static_analysis")

        self.assertIn(f"pip install -r {static_analysis.REQUIREMENTS}", job)
        self.assertIn("verify.py --step static_analysis", job)
        self.assertNotIn("if:", job)


if __name__ == "__main__":
    unittest.main()
