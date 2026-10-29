"""Tests for the time ceiling of the mutation step: passing it is a finding, never a slow run.

The clock is the boundary replaced here; each pair of readings brackets one tool run.
"""

from __future__ import annotations

from unittest import mock

from steps import mutation_policy

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
        with mock.patch(CLOCK, side_effect=[0.0, OVER, 0.0, 1.0, 0.0, 1.0]):
            result = self.run_step(healthy())

        self.assertFalse(result.ok)
        expected = "teto de 20 min excedido em `Core.Platform.Logging`: 21,0 min gastos"
        self.assertIn(f"núcleo (.NET): {expected}", result.details)

    def test_service_time_over_the_ceiling_fails_naming_the_module_and_time(self) -> None:
        with mock.patch(CLOCK, side_effect=[0.0, 1.0, 0.0, OVER, 0.0, 1.0]):
            result = self.run_step(healthy())

        self.assertFalse(result.ok)
        expected = "serviço (Python): teto de 20 min excedido em `language_service.auth`: 21,0 min"
        self.assertIn(f"{expected} gastos", result.details)

    def test_a_service_module_over_the_ceiling_does_not_stop_the_next_one(self) -> None:
        runner = healthy()

        with mock.patch(CLOCK, side_effect=[0.0, 1.0, 0.0, OVER, 0.0, 1.0]):
            result = self.run_step(runner)

        self.assertEqual(len(runner.called("--max-children")), 2)
        self.assertFalse(any("não rodaram" in line for line in result.details))

    def test_the_time_of_the_service_modules_does_not_add_up(self) -> None:
        with mock.patch(CLOCK, side_effect=[0.0, 1.0, 0.0, 12 * 60.0, 0.0, 12 * 60.0]):
            result = self.run_step(healthy())

        self.assertTrue(result.ok)

    def test_each_service_module_is_cut_at_the_whole_ceiling(self) -> None:
        runner = healthy()

        with mock.patch(CLOCK, side_effect=[0.0, 1.0, 0.0, UNDER, 0.0, 1.0]):
            self.run_step(runner)

        limits = [
            s for c, s in zip(runner.commands, runner.limits, strict=True) if "--max-children" in c
        ]
        self.assertEqual(limits, [20 * 60.0, 20 * 60.0])

    def test_time_under_the_ceiling_passes_and_is_reported(self) -> None:
        with mock.patch(CLOCK, side_effect=[0.0, UNDER, 0.0, UNDER, 0.0, UNDER]):
            result = self.run_step(healthy())

        self.assertTrue(result.ok)
        self.assertIn("  tempo: 5,0 min de 20 min de teto", result.details)

    def test_time_exactly_at_the_ceiling_passes(self) -> None:
        with mock.patch(CLOCK, side_effect=[0.0, 20 * 60.0, 0.0, 1.0, 0.0, 1.0]):
            result = self.run_step(healthy())

        self.assertTrue(result.ok)

    def test_the_ceiling_comes_from_the_document(self) -> None:
        document = mutation_samples.POLICY.replace("**20 minutos por", "**30 minutos por")
        support.write(self.root, mutation_policy.DOCUMENT, document)

        with mock.patch(CLOCK, side_effect=[0.0, OVER, 0.0, 1.0, 0.0, 1.0]):
            result = self.run_step(healthy())

        self.assertTrue(result.ok)

    def test_a_core_module_over_the_ceiling_does_not_stop_the_next_one(self) -> None:
        support.write(self.root, "core/src/Core.Boundary.Text/Core.Boundary.Text.csproj", "<P />")
        support.write(
            self.root, "core/tests/Core.Boundary.Tests/Core.Boundary.Tests.csproj", TEXT_REFERENCE
        )
        runner = healthy()

        with mock.patch(CLOCK, side_effect=[0.0, OVER, 0.0, 1.0, 0.0, 1.0, 0.0, 1.0]):
            result = self.run_step(runner)

        self.assertEqual(len(runner.called("stryker")), 2)
        self.assertFalse(any("não rodaram" in line for line in result.details))

    def test_each_core_module_is_cut_at_the_whole_ceiling(self) -> None:
        support.write(self.root, "core/src/Core.Boundary.Text/Core.Boundary.Text.csproj", "<P />")
        support.write(
            self.root, "core/tests/Core.Boundary.Tests/Core.Boundary.Tests.csproj", TEXT_REFERENCE
        )
        runner = healthy()

        with mock.patch(CLOCK, side_effect=[0.0, UNDER, 0.0, 1.0, 0.0, 1.0, 0.0, 1.0]):
            self.run_step(runner)

        self.assertEqual(runner.limits[1:3], [20 * 60.0, 20 * 60.0])
