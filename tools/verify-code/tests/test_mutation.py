"""Tests for the mutation step: the scope comes from docs/operacao/014, the tools are played."""

from __future__ import annotations

import json
from unittest import mock

from steps import areas_doc

from . import mutation_fixture as fixture
from . import mutation_samples, support
from .mutation_fixture import ToolRunner
from .mutation_samples import DOCUMENT, meta, stryker_report

CORE_CONFIG = ".verify-code/mutation/core/Core.Platform.Logging/stryker-config.json"
SERVICE_CONFIG = ".verify-code/mutation/service/setup.cfg"
AUTH_META = "src/language_service/auth.py"
LOGGING_META = "src/language_service/logging_/__init__.py"
KILLED_AUTH = "language_service.auth.x_accepts__mutmut_1"
SURVIVOR_AUTH = "language_service.auth.xǁVerifierǁaccepts__mutmut_2"
KILLED_LOGGING = "language_service.logging_.x_install__mutmut_1"


def healthy(stryker: str = "") -> ToolRunner:
    """Both tools report every mutant killed, unless a Stryker report is given."""
    return ToolRunner(
        stryker=stryker or stryker_report("ignored", "Killed"),
        metas={AUTH_META: meta({KILLED_AUTH: 1}), LOGGING_META: meta({KILLED_LOGGING: 1})},
    )


class MutationStepTests(fixture.MutationFixture):
    def test_stryker_mutates_exactly_the_folder_of_the_critical_core_module(self) -> None:
        self.run_step(healthy())

        config = json.loads((self.root / CORE_CONFIG).read_text(encoding="utf-8"))
        self.assertEqual(config["stryker-config"]["project"], "Core.Platform.csproj")
        self.assertEqual(config["stryker-config"]["mutate"], ["Logging/**/*.cs"])

    def test_stryker_runs_only_the_test_projects_that_reference_the_module(self) -> None:
        self.run_step(healthy())

        config = json.loads((self.root / CORE_CONFIG).read_text(encoding="utf-8"))
        expected = str(self.root / "core/tests/Core.Platform.Tests/Core.Platform.Tests.csproj")
        self.assertEqual(config["stryker-config"]["test-projects"], [expected])

    def test_stryker_runs_from_a_test_project_folder_not_beside_the_solution(self) -> None:
        runner = healthy()

        self.run_step(runner)

        stryker = runner.commands.index(runner.called("stryker")[0])
        self.assertEqual(runner.folders[stryker], self.root / "core/tests/Core.Platform.Tests")

    def test_a_module_no_test_project_references_fails_without_running_stryker(self) -> None:
        tests = "core/tests/Core.Platform.Tests/Core.Platform.Tests.csproj"
        support.write(self.root, tests, "<Project />")
        runner = healthy()

        result = self.run_step(runner)

        self.assertEqual(runner.called("stryker"), [])
        expected = "nenhum projeto de core/tests/ referencia Core.Platform"
        self.assertIn(f"Registro do núcleo (`Core.Platform.Logging`): {expected}", result.details)

    def test_stryker_runs_once_per_critical_core_module_with_code(self) -> None:
        runner = healthy()

        self.run_step(runner)

        self.assertEqual(len(runner.called("stryker")), 1)

    def test_mutmut_mutates_exactly_the_critical_service_modules(self) -> None:
        self.run_step(healthy())

        expected = (
            "[mutmut]\nsource_paths = src\nonly_mutate =\n    src/language_service/auth.py\n"
            "    src/language_service/logging_/*\npytest_add_cli_args =\n"
            "    --ignore=tests/architecture\n"
        )
        self.assertEqual((self.root / SERVICE_CONFIG).read_text(encoding="utf-8"), expected)

    def test_an_area_taken_off_the_floor_of_the_list_is_not_mutated(self) -> None:
        document = DOCUMENT.replace("Logging` | 90 %", "Logging` | 80 %")
        support.write(self.root, areas_doc.DOCUMENT, document)
        runner = healthy()

        self.run_step(runner)

        self.assertEqual(runner.called("stryker"), [])

    def test_every_module_killed_passes_and_names_its_counts(self) -> None:
        result = self.run_step(healthy())

        self.assertTrue(result.ok)
        expected = "Autenticação (`language_service.auth`): 1 gerados, 1 mortos, 0 sobreviventes"
        self.assertIn(f"{expected} (0,0 %)", result.details)

    def test_a_critical_area_without_code_is_named_and_does_not_fail(self) -> None:
        result = self.run_step(healthy())

        self.assertTrue(result.ok)
        expected = "Texto (`Core.Boundary.Text`): sem código; a mutação vale quando houver"
        self.assertIn(expected, result.details)

    def test_an_area_whose_folder_is_absent_from_its_project_is_without_code(self) -> None:
        row = "| Rede | `Core.Platform.Network` | 90 % | ZQX |\n"
        document = DOCUMENT.replace("| Serviço |", f"{row}| Serviço |")
        support.write(self.root, areas_doc.DOCUMENT, document)
        runner = healthy()

        result = self.run_step(runner)

        self.assertEqual(len(runner.called("stryker")), 1)
        expected = "Rede (`Core.Platform.Network`): sem código; a mutação vale quando houver"
        self.assertIn(expected, result.details)

    def test_a_core_survivor_is_named_by_file_line_and_change(self) -> None:
        report = stryker_report(self.logging_file, "Killed", "Survived")

        result = self.run_step(healthy(report))

        place = "core/src/Core.Platform/Logging/Formatter.cs:11"
        self.assertIn(f"  sobrevivente: {place} — Equality mutation: `a != b`", result.details)

    def test_a_service_survivor_is_named_by_file_line_and_change(self) -> None:
        runner = healthy()
        runner.metas[AUTH_META] = meta({KILLED_AUTH: 1, SURVIVOR_AUTH: 0})

        result = self.run_step(runner)

        place = "language-service/src/language_service/auth.py:2"
        change = "`return given == self.secret` → `return given != self.secret`"
        self.assertIn(f"  sobrevivente: {place} — {change}", result.details)

    def test_a_module_above_the_survivor_ceiling_fails_naming_it(self) -> None:
        runner = healthy()
        runner.metas[AUTH_META] = meta({KILLED_AUTH: 1, SURVIVOR_AUTH: 0})

        result = self.run_step(runner)

        self.assertFalse(result.ok)
        expected = "Autenticação (`language_service.auth`): 50,0 % de sobreviventes passa do"
        self.assertIn(f"{expected} limite de 20 %", result.details)

    def test_a_critical_area_no_tool_reaches_fails_naming_it(self) -> None:
        row = "| Ferramentas | `tools` | 90 % | ZQX |\n"
        document = DOCUMENT.replace("| Serviço |", f"{row}| Serviço |")
        support.write(self.root, areas_doc.DOCUMENT, document)

        result = self.run_step(healthy())

        self.assertFalse(result.ok)
        self.assertIn("`tools`: nenhuma ferramenta de mutação o alcança", result.details)

    def test_a_document_without_the_mutation_rules_fails_before_any_tool_runs(self) -> None:
        support.write(self.root, areas_doc.DOCUMENT, DOCUMENT.replace(mutation_samples.POLICY, ""))
        runner = healthy()

        result = self.run_step(runner)

        self.assertFalse(result.ok)
        self.assertEqual(runner.commands, [])

    def test_an_unrestorable_stryker_fails_naming_the_manifest(self) -> None:
        runner = healthy()
        runner.failing = ("restore",)

        result = self.run_step(runner)

        self.assertFalse(result.ok)
        self.assertIn("core/dotnet-tools.json não está disponível", result.details[1])

    def test_a_missing_stryker_report_fails_naming_the_module(self) -> None:
        runner = healthy()
        runner.stryker = ""

        result = self.run_step(runner)

        expected = "Registro do núcleo (`Core.Platform.Logging`): o Stryker não escreveu relatório"
        self.assertIn(expected, result.details)

    def test_a_missing_uv_fails_naming_how_to_install_it(self) -> None:
        runner = healthy()
        runner.failing = ("--max-children",)

        result = self.run_step(runner)

        self.assertFalse(result.ok)
        install = "instale com `pip install -r tools/verify-code/service-requirements.txt`"
        self.assertIn(f"serviço (Python): uv ausente? {install}", result.details)

    def test_outside_linux_the_service_side_is_skipped_with_a_warning(self) -> None:
        runner = healthy()

        with mock.patch("steps.mutation_service.sys.platform", "win32"):
            result = self.run_step(runner)

        self.assertTrue(result.ok)
        warning = "AVISO — serviço (Python): o mutmut só roda no Linux; pulado em win32"
        self.assertIn(warning, result.details)
