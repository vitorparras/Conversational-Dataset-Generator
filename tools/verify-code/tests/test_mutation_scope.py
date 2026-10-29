"""Tests for what keeps the mutation scope where docs/operacao/014 puts it, and nowhere else."""

from __future__ import annotations

import json

from steps import areas_doc, mutation_core, mutation_policy

from . import mutation_fixture as fixture
from . import support
from .mutation_samples import (
    AWAITED,
    DOCUMENT,
    POLICY,
    awaited_pair,
    meta,
    stryker_report,
    stryker_with_source,
)
from .test_mutation import AUTH_META, KILLED_AUTH, SURVIVOR_AUTH, healthy

LOGGING_CONFIG = ".verify-code/mutation/core/Core.Platform.Logging/stryker-config.json"
PLATFORM_CONFIG = ".verify-code/mutation/core/Core.Platform/stryker-config.json"
INNER_META = "src/language_service/logging_/inner.py"
INNER_ROW = "| Interno | `language_service.logging_.inner` | 80 % | não | ZQX |\n"


class MutationScopeTests(fixture.MutationFixture):
    def replace_in_document(self, old: str, new: str) -> None:
        support.write(self.root, areas_doc.DOCUMENT, DOCUMENT.replace(old, new))

    def test_a_tool_configuration_in_the_service_project_fails_before_mutmut_runs(self) -> None:
        support.write(self.root, "language-service/pyproject.toml", "[tool.mutmut]\nx = 1\n")
        runner = healthy()

        result = self.run_step(runner)

        self.assertFalse(result.ok)
        self.assertEqual(runner.called("--max-children"), [])
        expected = "language-service/pyproject.toml tem `[tool.mutmut]`; o escopo vem de"
        self.assertIn(f"serviço (Python): {expected} {areas_doc.DOCUMENT}", result.details)

    def test_a_tool_configuration_in_a_nested_project_of_the_service_fails(self) -> None:
        support.write(self.root, "language-service/tests/sub/pyproject.toml", "[tool.mutmut]\n")

        result = self.run_step(healthy())

        self.assertFalse(result.ok)

    def test_a_document_with_no_critical_area_fails_before_any_tool_runs(self) -> None:
        support.write(self.root, areas_doc.DOCUMENT, DOCUMENT.replace("| sim |", "| não |"))
        runner = healthy()

        result = self.run_step(runner)

        self.assertEqual(result.details, (f"{areas_doc.DOCUMENT}: nenhuma área com Crítica `sim`",))
        self.assertEqual(runner.commands, [])

    def test_a_critical_area_with_a_floor_of_95_is_mutated_and_does_not_fail(self) -> None:
        self.replace_in_document("Logging` | 90 %", "Logging` | 95 %")
        runner = healthy()

        result = self.run_step(runner)

        self.assertTrue(result.ok)
        self.assertEqual(len(runner.called("stryker")), 1)

    def test_a_stryker_error_fails_even_with_a_report_written(self) -> None:
        runner = healthy(stryker_report(self.logging_file, "Killed"))
        runner.failing = ("--config-file",)

        result = self.run_step(runner)

        self.assertIn(
            "Registro do núcleo (`Core.Platform.Logging`): o Stryker terminou com erro",
            result.details,
        )

    def test_the_project_with_the_longest_name_owns_the_area(self) -> None:
        support.write(self.root, "core/src/Core.Platform.Logging/Core.Platform.Logging.csproj", "")

        result = self.run_step(healthy())

        expected = "nenhum projeto de core/tests/ referencia Core.Platform.Logging"
        self.assertIn(f"Registro do núcleo (`Core.Platform.Logging`): {expected}", result.details)

    def test_a_critical_project_leaves_out_the_folders_of_the_areas_nested_in_it(self) -> None:
        self.replace_in_document("`Core.Platform` | 80 % | não", "`Core.Platform` | 80 % | sim")

        self.run_step(healthy())

        config = json.loads((self.root / PLATFORM_CONFIG).read_text(encoding="utf-8"))
        self.assertEqual(config["stryker-config"]["mutate"], ["**/*.cs", "!Logging/**/*.cs"])

    def test_a_nested_critical_core_area_is_mutated_by_itself_and_left_out_of_its_container(
        self,
    ) -> None:
        self.replace_in_document("`Core.Platform` | 80 % | não", "`Core.Platform` | 80 % | sim")

        self.run_step(healthy())

        platform = json.loads((self.root / PLATFORM_CONFIG).read_text(encoding="utf-8"))
        logging = json.loads((self.root / LOGGING_CONFIG).read_text(encoding="utf-8"))
        self.assertEqual(platform["stryker-config"]["mutate"], ["**/*.cs", "!Logging/**/*.cs"])
        self.assertEqual(logging["stryker-config"]["mutate"], ["Logging/**/*.cs"])

    def test_a_service_mutant_owned_by_a_nested_area_is_not_counted(self) -> None:
        self.replace_in_document("| Serviço |", f"{INNER_ROW}| Serviço |")
        runner = healthy()
        runner.metas[INNER_META] = meta({"language_service.logging_.inner.x_f__mutmut_1": 0})

        result = self.run_step(runner)

        expected = "Registro do serviço (`language_service.logging_`): 1 gerados, 1 mortos"
        self.assertIn(f"{expected}, 0 sobreviventes (0,0 %)", result.details)

    def test_lines_switched_off_by_pragma_are_named_per_module(self) -> None:
        source = "x = 1  # pragma: no mutate\n"
        support.write(self.root, "language-service/src/language_service/logging_/a.py", source)

        result = self.run_step(healthy())

        expected = "Registro do serviço (`language_service.logging_`): 1 `pragma: no mutate`"
        self.assertIn(f"{expected} fora da mutação", result.details)

    def test_a_pragma_of_a_nested_critical_area_is_named_once_under_the_nested_area(self) -> None:
        inner = INNER_ROW.replace("80 % | não", "90 % | sim")
        self.replace_in_document("| Serviço |", f"{inner}| Serviço |")
        source = "language-service/src/language_service/logging_/inner.py"
        support.write(self.root, source, "x = 1  # pragma: no mutate\n")
        test = "language-service/tests/unit/test_inner.py"
        support.write(self.root, test, "import language_service.logging_.inner\n")

        result = self.run_step(healthy())

        lines = [line for line in result.details if "`pragma: no mutate` fora da mutação" in line]
        self.assertEqual(len(lines), 1)
        self.assertIn("Interno (`language_service.logging_.inner`)", lines[0])

    def test_a_survivor_whose_diff_could_not_be_shown_is_named_unplaced(self) -> None:
        runner = healthy()
        runner.metas[AUTH_META] = meta({KILLED_AUTH: 1, SURVIVOR_AUTH: 0})
        runner.failing = ("show",)

        result = self.run_step(runner)

        place = "language-service/src/language_service/auth.py (linha não localizada)"
        self.assertIn(f"  sobrevivente: {place} — ", result.details[-4])

    def test_stryker_itself_never_reproves(self) -> None:
        self.run_step(healthy())

        config = json.loads((self.root / LOGGING_CONFIG).read_text(encoding="utf-8"))
        self.assertEqual(config["stryker-config"]["thresholds"]["break"], 0)

    def test_stryker_gets_no_method_filter_that_would_hide_the_call_chain(self) -> None:
        self.run_step(healthy())

        config = json.loads((self.root / LOGGING_CONFIG).read_text(encoding="utf-8"))
        self.assertNotIn("ignore-methods", config["stryker-config"])

    def test_a_statement_mutation_on_a_configure_await_line_survives_in_the_step(self) -> None:
        statement, argument = awaited_pair("Survived")
        report = stryker_with_source(self.logging_file, AWAITED, statement, argument)

        result = self.run_step(healthy(report))

        place = "core/src/Core.Platform/Logging/Formatter.cs:1"
        self.assertIn(f"  sobrevivente: {place} — Statement mutation: `;`", result.details)
        self.assertNotIn(f"  sobrevivente: {place} — Boolean mutation: `true`", result.details)

    def test_the_method_whose_argument_is_left_out_follows_the_document(self) -> None:
        _, argument = awaited_pair("Survived")
        report = stryker_with_source(self.logging_file, AWAITED, argument)
        other = POLICY.replace("`ConfigureAwait`", "`Outro`")
        support.write(self.root, mutation_policy.DOCUMENT, other)

        result = self.run_step(healthy(report))

        place = "core/src/Core.Platform/Logging/Formatter.cs:1"
        self.assertIn(f"  sobrevivente: {place} — Boolean mutation: `true`", result.details)

    def test_a_test_project_of_a_project_with_a_longer_name_is_not_taken(self) -> None:
        other = '<ProjectReference Include="../../src/Other.Core.Platform.csproj" />'
        support.write(self.root, "core/tests/Other.Tests/Other.Tests.csproj", other)

        self.run_step(healthy())

        config = json.loads((self.root / LOGGING_CONFIG).read_text(encoding="utf-8"))
        self.assertEqual(len(config["stryker-config"]["test-projects"]), 1)

    def test_a_cross_cutting_suite_that_references_the_module_is_not_taken(self) -> None:
        reference = '<ProjectReference Include="../../src/Core.Platform/Core.Platform.csproj" />'
        support.write(
            self.root, "core/tests/Core.Tests.Integration/Core.Tests.Integration.csproj", reference
        )

        self.run_step(healthy())

        config = json.loads((self.root / LOGGING_CONFIG).read_text(encoding="utf-8"))
        expected = str(self.root / "core/tests/Core.Platform.Tests/Core.Platform.Tests.csproj")
        self.assertEqual(config["stryker-config"]["test-projects"], [expected])

    def test_a_project_nested_in_another_takes_the_suite_of_the_one_it_sits_in(self) -> None:
        reference = '<ProjectReference Include="../../src/Core.Boundary.Contracts.csproj" />'
        support.write(
            self.root, "core/tests/Core.Boundary.Tests/Core.Boundary.Tests.csproj", reference
        )
        support.write(
            self.root, "core/tests/Core.Tests.Properties/Core.Tests.Properties.csproj", reference
        )

        found = mutation_core.test_projects(self.root, "Core.Boundary.Contracts")

        self.assertEqual([p.parent.name for p in found], ["Core.Boundary.Tests"])

    def test_a_module_without_a_database_runs_stryker_with_one_worker(self) -> None:
        self.run_step(healthy())

        config = json.loads((self.root / LOGGING_CONFIG).read_text(encoding="utf-8"))
        self.assertEqual(config["stryker-config"]["concurrency"], 1)

    def test_a_module_whose_test_project_starts_the_database_runs_stryker_with_one_worker(
        self,
    ) -> None:
        tests = self.root / "core/tests/Core.Platform.Tests/Core.Platform.Tests.csproj"
        reference = tests.read_text(encoding="utf-8")
        database = '<PackageReference Include="Testcontainers" />'
        support.write(
            self.root,
            "core/tests/Core.Platform.Tests/Core.Platform.Tests.csproj",
            reference + database,
        )

        self.run_step(healthy())

        config = json.loads((self.root / LOGGING_CONFIG).read_text(encoding="utf-8"))
        self.assertEqual(config["stryker-config"]["concurrency"], 1)
