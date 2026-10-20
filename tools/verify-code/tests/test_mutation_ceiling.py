"""Tests for the time ceiling of the mutation step: passing it is a finding, never a slow run.

The clock is the boundary replaced here; each pair of readings brackets one tool run.
"""

from __future__ import annotations

from unittest import mock

from . import mutation_fixture as fixture
from . import mutation_samples, support
from .test_mutation import healthy

CLOCK = "steps.mutation_judge.monotonic"
OVER = 21 * 60.0
UNDER = 5 * 60.0
TEXT_REFERENCE = (
    '<ProjectReference Include="..\\..\\src\\Core.Boundary.Text\\Core.Boundary.Text.csproj" />'
)


class MutationCeilingTests(fixture.MutationFixture):
    def test_core_time_over_the_ceiling_fails_naming_module_and_time(self) -> None:
        with mock.patch(CLOCK, side_effect=[0.0, OVER, 0.0, 1.0]):
            result = self.run_step(healthy())

        self.assertFalse(result.ok)
        expected = "teto de 20 min excedido em `Core.Platform.Logging`: 21,0 min gastos"
        self.assertIn(f"núcleo (.NET): {expected}", result.details)

    def test_service_time_over_the_ceiling_fails_naming_its_modules_and_time(self) -> None:
        with mock.patch(CLOCK, side_effect=[0.0, 1.0, 0.0, OVER]):
            result = self.run_step(healthy())

        self.assertFalse(result.ok)
        modules = "`language_service.auth`, `language_service.logging_`"
        expected = f"serviço (Python): teto de 20 min excedido em {modules}: 21,0 min gastos"
        self.assertIn(expected, result.details)

    def test_time_under_the_ceiling_passes_and_is_reported(self) -> None:
        with mock.patch(CLOCK, side_effect=[0.0, UNDER, 0.0, UNDER]):
            result = self.run_step(healthy())

        self.assertTrue(result.ok)
        self.assertIn("núcleo (.NET): 5,0 min de 20 min de teto", result.details)

    def test_time_exactly_at_the_ceiling_passes(self) -> None:
        with mock.patch(CLOCK, side_effect=[0.0, 20 * 60.0, 0.0, 1.0]):
            result = self.run_step(healthy())

        self.assertTrue(result.ok)

    def test_the_ceiling_comes_from_the_document(self) -> None:
        document = mutation_samples.DOCUMENT.replace("**20 minutos por", "**30 minutos por")
        support.write(self.root, "docs/operacao/014-modulos-criticos-e-pisos.md", document)

        with mock.patch(CLOCK, side_effect=[0.0, OVER, 0.0, 1.0]):
            result = self.run_step(healthy())

        self.assertTrue(result.ok)

    def test_core_modules_after_the_ceiling_do_not_run_and_are_named(self) -> None:
        support.write(self.root, "core/src/Core.Boundary.Text/Core.Boundary.Text.csproj", "<P />")
        runner = healthy()

        with mock.patch(CLOCK, side_effect=[0.0, OVER, 0.0, 1.0]):
            result = self.run_step(runner)

        self.assertEqual(len(runner.called("stryker")), 1)
        expected = "núcleo (.NET): não rodaram por causa do teto: `Core.Boundary.Text`"
        self.assertIn(expected, result.details)

    def test_each_core_module_is_cut_at_what_is_left_of_the_ceiling(self) -> None:
        support.write(self.root, "core/src/Core.Boundary.Text/Core.Boundary.Text.csproj", "<P />")
        support.write(self.root, "core/tests/T/T.csproj", TEXT_REFERENCE)
        runner = healthy()

        with mock.patch(CLOCK, side_effect=[0.0, UNDER, 0.0, 1.0, 0.0, 1.0]):
            self.run_step(runner)

        self.assertEqual(runner.limits[1:3], [20 * 60.0, 15 * 60.0])

    def test_the_service_run_is_cut_at_the_ceiling(self) -> None:
        runner = healthy()

        with mock.patch(CLOCK, side_effect=[0.0, 1.0, 0.0, 1.0]):
            self.run_step(runner)

        mutmut = runner.commands.index(runner.called("--max-children")[0])
        self.assertEqual(runner.limits[mutmut], 20 * 60.0)
