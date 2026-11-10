"""Tests for test_core: environment, empty run, silent project, failure, skips, coverage."""

from __future__ import annotations

import os
import sys
import tempfile
import unittest
from collections.abc import Sequence
from pathlib import Path

from steps import test_core
from steps.common import CommandOutcome, run_process

from . import support


class TestCoreTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)
        support.write(self.root, test_core.SOLUTION, support.SOLUTION_WITH_TESTS)
        self.results = self.root / ".verify-code" / test_core.RESULTS

    def runner(self, files: dict[str, str], ok: bool = True) -> support.FakeRunner:
        return support.FakeRunner(CommandOutcome(ok, "dotnet output"), files, self.results)

    def test_child_process_receives_the_telemetry_opt_out(self) -> None:
        script = "import os; print(os.environ['DOTNET_CLI_TELEMETRY_OPTOUT'])"

        outcome = run_process([sys.executable, "-c", script], self.root)

        self.assertTrue(outcome.ok, outcome.text)
        self.assertEqual(outcome.text.strip(), "1")

    def test_missing_solution_fails(self) -> None:
        (self.root / test_core.SOLUTION).unlink()

        result = test_core.run(support.context(self.root, self.runner({})))

        self.assertFalse(result.ok)
        self.assertIn("não há suíte", result.details[0])

    def test_no_report_written_fails_instead_of_passing_empty(self) -> None:
        result = test_core.run(support.context(self.root, self.runner({})))

        self.assertFalse(result.ok)
        self.assertIn(
            "nenhum relatório de teste foi escrito: o passo não passa vazio", result.details
        )

    def test_project_that_executed_no_test_fails_by_name(self) -> None:
        files = {
            "a.trx": support.trx("Alpha.Tests", 3, 0),
            "b.trx": support.trx("Beta.Tests", 0, 0),
        }

        result = test_core.run(support.context(self.root, self.runner(files)))

        self.assertFalse(result.ok)
        self.assertIn("o projeto de teste Beta.Tests não executou nenhum teste", result.details)

    def test_a_library_that_is_no_test_project_is_not_asked_to_run_tests(self) -> None:
        solution = self.root / test_core.SOLUTION
        solution.write_text(
            solution.read_text(encoding="utf-8").replace(
                "</Solution>", '  <Project Path="tests/Support/Support.csproj" />\n</Solution>'
            ),
            encoding="utf-8",
        )
        support.write(
            self.root,
            "core/tests/Support/Support.csproj",
            "<Project><PropertyGroup><IsTestProject>false</IsTestProject></PropertyGroup></Project>",
        )
        files = {
            "a.trx": support.trx("Alpha.Tests", 3, 0),
            "b.trx": support.trx("Beta.Tests", 2, 0),
            "c.cobertura.xml": "<coverage/>",
        }

        result = test_core.run(support.context(self.root, self.runner(files)))

        self.assertTrue(result.ok, result.details)

    def test_a_failed_test_fails_the_step(self) -> None:
        files = {
            "a.trx": support.trx("Alpha.Tests", 3, 1),
            "b.trx": support.trx("Beta.Tests", 2, 0),
        }

        result = test_core.run(support.context(self.root, self.runner(files, ok=False)))

        self.assertFalse(result.ok)
        self.assertIn("1 teste(s) do núcleo falharam", result.details)

    def test_a_failed_test_is_named_with_the_first_line_of_its_message_and_the_rest_follows(
        self,
    ) -> None:
        files = {
            "a.trx": support.trx_failing(
                "Alpha.Tests", 3, "Ns.Classe.Metodo", "Esperado 1\nmas foi 2\n  em x.cs:9"
            ),
            "b.trx": support.trx("Beta.Tests", 2, 0),
        }

        result = test_core.run(support.context(self.root, self.runner(files, ok=False)))

        named = result.details.index("  Alpha.Tests: Ns.Classe.Metodo: Esperado 1")
        self.assertEqual(
            ("    | mas foi 2", "    |   em x.cs:9"), result.details[named + 1 : named + 3]
        )

    def test_the_whole_message_of_a_failed_test_reaches_the_report(self) -> None:
        message = "dotnet publish exited with 1: Killed\n" + "\n".join(
            f"line {number}" for number in range(1, 51)
        )
        files = {
            "a.trx": support.trx_failing("Alpha.Tests", 3, "Ns.Classe.Metodo", message),
            "b.trx": support.trx("Beta.Tests", 2, 0),
        }

        result = test_core.run(support.context(self.root, self.runner(files, ok=False)))

        self.assertIn(
            "  Alpha.Tests: Ns.Classe.Metodo: dotnet publish exited with 1: Killed", result.details
        )
        self.assertIn("    | line 1", result.details)
        self.assertIn("    | line 50", result.details)

    def test_a_message_longer_than_the_limit_keeps_its_end_and_says_how_much_was_left_out(
        self,
    ) -> None:
        total = test_core.MESSAGE_LINES + 15
        message = "headline\n" + "\n".join(f"line {number}" for number in range(1, total + 1))
        files = {
            "a.trx": support.trx_failing("Alpha.Tests", 3, "Ns.Classe.Metodo", message),
            "b.trx": support.trx("Beta.Tests", 2, 0),
        }

        result = test_core.run(support.context(self.root, self.runner(files, ok=False)))

        self.assertIn("    | (15 linha(s) anteriores omitidas)", result.details)
        self.assertNotIn("    | line 15", result.details)
        self.assertIn("    | line 16", result.details)
        self.assertIn(f"    | line {total}", result.details)

    def test_a_failed_test_without_message_is_still_named(self) -> None:
        files = {
            "a.trx": support.trx_failing("Alpha.Tests", 3, "Ns.Classe.Metodo", ""),
            "b.trx": support.trx("Beta.Tests", 2, 0),
        }

        result = test_core.run(support.context(self.root, self.runner(files, ok=False)))

        self.assertIn("  Alpha.Tests: Ns.Classe.Metodo: sem mensagem", result.details)

    def test_every_project_ran_and_coverage_was_written_passes(self) -> None:
        files = {
            "a.trx": support.trx("Alpha.Tests", 3, 0),
            "b.trx": support.trx("Beta.Tests", 2, 0),
            "x.cobertura.xml": "<coverage />",
        }

        result = test_core.run(support.context(self.root, self.runner(files)))

        self.assertTrue(result.ok, result.details)
        self.assertIn("5 testes executados em 2 projetos", result.details)

    def test_skipped_tests_are_reported_with_their_reason(self) -> None:
        files = {
            "a.trx": support.trx("Alpha.Tests", 3, 0, skipped="ZQX-MOTIVO"),
            "b.trx": support.trx("Beta.Tests", 2, 0),
            "x.cobertura.xml": "<coverage />",
        }

        result = test_core.run(support.context(self.root, self.runner(files)))

        self.assertTrue(result.ok, result.details)
        self.assertIn("Alpha.Tests: 1 teste(s) saltado(s) (ZQX-MOTIVO)", result.details)

    def test_skipped_tests_are_reported_when_the_step_fails(self) -> None:
        files = {
            "a.trx": support.trx("Alpha.Tests", 3, 1, skipped="ZQX-MOTIVO"),
            "b.trx": support.trx("Beta.Tests", 2, 0),
        }

        result = test_core.run(support.context(self.root, self.runner(files, ok=False)))

        self.assertFalse(result.ok)
        self.assertIn("Alpha.Tests: 1 teste(s) saltado(s) (ZQX-MOTIVO)", result.details)

    def test_no_skip_line_when_nothing_was_skipped(self) -> None:
        files = {
            "a.trx": support.trx("Alpha.Tests", 3, 0),
            "b.trx": support.trx("Beta.Tests", 2, 0),
            "x.cobertura.xml": "<coverage />",
        }

        result = test_core.run(support.context(self.root, self.runner(files)))

        self.assertEqual(result.details[:-1], ("5 testes executados em 2 projetos",))

    def test_missing_coverage_report_fails(self) -> None:
        files = {
            "a.trx": support.trx("Alpha.Tests", 3, 0),
            "b.trx": support.trx("Beta.Tests", 2, 0),
        }

        result = test_core.run(support.context(self.root, self.runner(files)))

        self.assertFalse(result.ok)
        self.assertIn("nenhum relatório de cobertura Cobertura foi escrito", result.details)

    def test_dotnet_test_error_without_failed_test_still_fails(self) -> None:
        files = {
            "a.trx": support.trx("Alpha.Tests", 3, 0),
            "b.trx": support.trx("Beta.Tests", 2, 0),
            "x.cobertura.xml": "<coverage />",
        }

        result = test_core.run(support.context(self.root, self.runner(files, ok=False)))

        self.assertFalse(result.ok)
        self.assertIn("terminou com erro", result.details[0])

    def test_results_of_an_earlier_run_do_not_count(self) -> None:
        self.results.mkdir(parents=True)
        (self.results / "old.trx").write_text(support.trx("Alpha.Tests", 9, 0), encoding="utf-8")

        result = test_core.run(support.context(self.root, self.runner({})))

        self.assertFalse(result.ok)
        self.assertIn(
            "nenhum relatório de teste foi escrito: o passo não passa vazio", result.details
        )

    def test_the_command_asks_for_cobertura_and_trx_reports(self) -> None:
        runner = self.runner({})

        test_core.run(support.context(self.root, runner))

        self.assertIn("--coverage-output-format", runner.commands[0])
        self.assertIn("--report-xunit-trx", runner.commands[0])

    def test_the_tests_that_start_the_real_service_are_left_out(self) -> None:
        runner = self.runner({})

        test_core.run(support.context(self.root, runner))

        command = runner.commands[0]
        index = command.index("--filter-not-trait")
        self.assertEqual("Category=RealService", command[index + 1])
        self.assertNotIn("--filter-trait", command)


class MsBuildSummaryTests(unittest.TestCase):
    LOG = (
        "[msbuild] Core.Host.csproj -t:Compile 2.5s\nruído\n[msbuild] App.csproj -t:Restore 9.0s\n"
    )

    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)
        support.write(self.root, test_core.SOLUTION, support.SOLUTION_WITH_TESTS)
        self.results = self.root / ".verify-code" / test_core.RESULTS

    def files(self, trx: str = "ok") -> dict[str, str]:
        failing = support.trx("Alpha.Tests", 3, 1 if trx == "failed" else 0)
        return {
            "a.trx": failing,
            "b.trx": support.trx("Beta.Tests", 2, 0),
            "x.cobertura.xml": "<coverage />",
            test_core.MSBUILD_LOG: self.LOG,
        }

    def run_step(self, files: dict[str, str], ok: bool = True) -> test_core.StepResult:
        runner = support.FakeRunner(CommandOutcome(ok, "dotnet output"), files, self.results)
        return test_core.run(support.context(self.root, runner))

    def test_the_summary_lists_each_msbuild_the_longest_and_the_total(self) -> None:
        result = self.run_step(self.files())

        self.assertTrue(result.ok, result.details)
        self.assertEqual(
            (
                "  msbuild Core.Host.csproj -t:Compile 2.5s",
                "  msbuild App.csproj -t:Restore 9.0s",
                "msbuild do Core.Tests.Architecture: 2 execuções, total 11.5s, "
                "maior App.csproj -t:Restore 9.0s",
            ),
            result.details[-3:],
        )

    def test_the_summary_is_printed_when_the_step_fails_too(self) -> None:
        result = self.run_step(self.files("failed"), ok=False)

        self.assertFalse(result.ok)
        self.assertIn("1 teste(s) do núcleo falharam", result.details)
        self.assertIn(
            "msbuild do Core.Tests.Architecture: 2 execuções, total 11.5s, "
            "maior App.csproj -t:Restore 9.0s",
            result.details,
        )

    def test_a_run_that_logged_no_msbuild_says_so(self) -> None:
        files = {name: text for name, text in self.files().items() if name != test_core.MSBUILD_LOG}

        result = self.run_step(files)

        self.assertEqual(
            "msbuild do Core.Tests.Architecture: nenhuma execução registrada", result.details[-1]
        )

    def test_the_command_gets_the_log_in_the_results_directory_and_the_environment_is_put_back(
        self,
    ) -> None:
        seen: list[str | None] = []

        def runner(command: Sequence[str], cwd: Path) -> CommandOutcome:
            seen.append(os.environ.get(test_core.MSBUILD_LOG_VARIABLE))
            return CommandOutcome(True, "")

        test_core.run(support.context(self.root, runner))

        self.assertEqual([str(self.results / test_core.MSBUILD_LOG)], seen)
        self.assertNotIn(test_core.MSBUILD_LOG_VARIABLE, os.environ)

    def test_a_value_the_variable_had_before_is_put_back(self) -> None:
        os.environ[test_core.MSBUILD_LOG_VARIABLE] = "antes"
        self.addCleanup(os.environ.pop, test_core.MSBUILD_LOG_VARIABLE, None)

        self.run_step(self.files())

        self.assertEqual("antes", os.environ[test_core.MSBUILD_LOG_VARIABLE])
