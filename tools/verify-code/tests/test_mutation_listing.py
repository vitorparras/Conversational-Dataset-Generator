"""Tests for the list of mutation jobs of a pull request: of the modules the diff reaches, only
those with a file of their own in the diff run, and the reasons say, per module, what is mutated
now and what waits for the nightly run of main."""

from __future__ import annotations

from unittest import mock

from steps import mutation_diff
from steps.common import OUTPUT_DIRECTORY, Context

from . import mutation_fixture as fixture
from . import support
from .test_mutation_diff import SERVICE_SINK, SINK, runner

LOGGING = "Core.Platform.Logging"
SERVICE = "language_service.logging_"
FORMATTER = "core/src/Core.Platform/Logging/Formatter.cs"
REACHED = ((LOGGING, SERVICE), ["- `Core.Platform.Logging`: arquivo de Core.Platform"])


class ListingTests(fixture.MutationFixture):
    def setUp(self) -> None:
        super().setUp()
        support.write(self.root, SINK, "class S {}\n")
        support.write(self.root, SERVICE_SINK, "def write(x):\n    return x + 1\n")

    def select(self, *changed: str, unreadable: bool = False) -> tuple[tuple[str, ...], list[str]]:
        tools = runner(*changed, unreadable=unreadable)
        context = Context(self.root, self.root / OUTPUT_DIRECTORY, tools, "origin/main")
        with mock.patch.object(mutation_diff.reach, "select", return_value=REACHED):
            return mutation_diff.select(context)

    def test_only_the_reached_modules_with_a_file_in_the_diff_get_a_job(self) -> None:
        names, _ = self.select(SINK)

        self.assertEqual(names, (LOGGING,))

    def test_the_reasons_name_the_files_mutated_and_the_ones_left_for_the_night(self) -> None:
        _, lines = self.select(SINK)

        expected = (
            f"- `{LOGGING}`: muta 1 de 2 arquivos ({SINK}); "
            "os outros ficam para a mutação noturna do main"
        )
        self.assertIn(expected, lines)

    def test_a_reached_module_with_no_file_in_the_diff_says_it_waits_for_the_night(self) -> None:
        _, lines = self.select(SINK)

        expected = f"- `{SERVICE}`: nenhum arquivo do módulo no diff; fica para a mutação noturna"
        self.assertTrue(any(line.startswith(expected) for line in lines), lines)

    def test_a_module_whole_in_the_diff_says_so(self) -> None:
        _, lines = self.select(SINK, FORMATTER)

        expected = f"- `{LOGGING}`: muta 2 de 2 arquivos ({FORMATTER}, {SINK}); o módulo inteiro"
        self.assertTrue(any(line.startswith(expected) for line in lines), lines)

    def test_the_reasons_of_the_reach_stay_first(self) -> None:
        _, lines = self.select(SINK)

        self.assertEqual(lines[0], REACHED[1][0])

    def test_a_diff_that_cannot_be_read_keeps_every_reached_job(self) -> None:
        names, lines = self.select(SINK, unreadable=True)

        self.assertEqual(names, REACHED[0])
        self.assertEqual(lines, REACHED[1])

    def test_a_diff_that_reaches_no_module_lists_none(self) -> None:
        tools = runner("README.md")
        context = Context(self.root, self.root / OUTPUT_DIRECTORY, tools, "origin/main")
        nothing = ((), ["nenhum módulo alcançado"])
        with mock.patch.object(mutation_diff.reach, "select", return_value=nothing):
            names, lines = mutation_diff.select(context)

        self.assertEqual((names, lines), nothing)

