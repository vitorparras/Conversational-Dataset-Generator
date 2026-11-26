"""Tests for the mutation of a pull request: with a base, the step mutates only the files of each
critical module the diff changed, and says which; without one, the whole module, as at night."""

from __future__ import annotations

import json
from collections.abc import Sequence
from dataclasses import dataclass, field
from pathlib import Path

from steps.common import OUTPUT_DIRECTORY, CommandOutcome, Context, StepResult
from steps.mutation import run
from steps.mutation_parts import Slice, portion

from . import mutation_fixture as fixture
from . import support
from .mutation_fixture import ToolRunner
from .mutation_samples import meta, stryker_report

CORE_CONFIG = ".verify-code/mutation/core/Core.Platform.Logging/stryker-config.json"
LOGGING_CONFIG = ".verify-code/mutation/service/language_service.logging_/setup.cfg"
SINK = "core/src/Core.Platform/Logging/Sink.cs"
SERVICE_SINK = "language-service/src/language_service/logging_/sink.py"
LOGGING_META = "src/language_service/logging_/sink.py"
KILLED_LOGGING = "language_service.logging_.sink.x_write__mutmut_1"
NIGHTLY = "o módulo inteiro roda na mutação noturna do main"


@dataclass
class DiffRunner(ToolRunner):
    """The tool runner, answering `git diff` with the paths of the pull request."""

    changed: tuple[str, ...] = ()
    unreadable: bool = False
    diffs: list[list[str]] = field(default_factory=list[list[str]])

    def __call__(self, command: Sequence[str], cwd: Path, seconds: float = 0.0) -> CommandOutcome:
        if command[:1] == ["git"] and "diff" in command:
            self.diffs.append(list(command))
            return CommandOutcome(not self.unreadable, "\n".join(self.changed))
        return super().__call__(command, cwd, seconds)


def runner(*changed: str, stryker: str = "", unreadable: bool = False) -> DiffRunner:
    return DiffRunner(
        stryker=stryker or stryker_report("ignored", "Killed"),
        metas={LOGGING_META: meta({KILLED_LOGGING: 1})},
        changed=changed,
        unreadable=unreadable,
    )


class StepDiffTests(fixture.MutationFixture):
    def setUp(self) -> None:
        super().setUp()
        support.write(self.root, SINK, "class S { int f = 0; }\n")
        support.write(self.root, SERVICE_SINK, "def write(x):\n    return x + 1\n")

    def step(self, tools: DiffRunner, module: str | None = None) -> StepResult:
        context = Context(
            self.root, self.root / OUTPUT_DIRECTORY, tools, "origin/main", None, tools, module
        )
        return run(context)

    def mutated(self) -> list[str]:
        config = json.loads((self.root / CORE_CONFIG).read_text(encoding="utf-8"))
        return config["stryker-config"]["mutate"]

    def test_with_a_base_stryker_mutates_only_the_file_the_diff_changed(self) -> None:
        self.step(runner(SINK, "README.md"))

        self.assertEqual(self.mutated(), ["Logging/Sink.cs"])

    def test_the_diff_is_read_against_the_base_from_the_merge_base(self) -> None:
        tools = runner(SINK)

        self.step(tools)

        self.assertIn("origin/main...HEAD", tools.diffs[0])

    def test_a_diff_that_cannot_be_read_mutates_the_whole_module_and_says_so(self) -> None:
        result = self.step(runner(SINK, unreadable=True))

        self.assertEqual(self.mutated(), ["Logging/**/*.cs"])
        self.assertTrue(any("módulo inteiro" in line for line in result.details), result.details)

    def test_the_report_names_the_files_mutated_and_says_the_rest_runs_at_night(self) -> None:
        result = self.step(runner(SINK), "Core.Platform.Logging")

        self.assertTrue(result.ok, result.details)
        named = "Registro do núcleo, só os arquivos do diff (`Core.Platform.Logging`): 1 gerados"
        self.assertTrue(any(line.startswith(named) for line in result.details), result.details)
        self.assertIn(f"`Core.Platform.Logging`: mutados {SINK}", result.details)
        self.assertTrue(any(NIGHTLY in line for line in result.details), result.details)

    def test_a_module_with_no_file_in_the_diff_is_not_mutated_and_passes_saying_so(self) -> None:
        tools = runner(SERVICE_SINK)

        result = self.step(tools, "Core.Platform.Logging")

        self.assertTrue(result.ok, result.details)
        self.assertEqual(tools.called("stryker"), [])
        expected = "`Core.Platform.Logging`: nenhum arquivo do módulo no diff; nada a mutar aqui"
        self.assertIn(expected, result.details)

    def test_with_a_base_mutmut_mutates_only_the_python_file_the_diff_changed(self) -> None:
        self.step(runner(SERVICE_SINK), "language_service.logging_")

        config = (self.root / LOGGING_CONFIG).read_text(encoding="utf-8")
        self.assertIn("only_mutate =\n    src/language_service/logging_/sink.py\n", config)
        self.assertNotIn("logging_/*", config)

    def test_a_changed_file_with_no_mutant_passes_with_a_note(self) -> None:
        empty = json.dumps({"schemaVersion": "2", "files": {}})

        result = self.step(runner(SINK, stryker=empty), "Core.Platform.Logging")

        self.assertTrue(result.ok, result.details)
        self.assertTrue(any("nenhum mutante nos arquivos do diff" in d for d in result.details))

    def test_the_survivor_limit_holds_for_the_files_of_the_diff(self) -> None:
        report = stryker_report(str(self.root / SINK), "Killed", "Survived")

        result = self.step(runner(SINK, stryker=report), "Core.Platform.Logging")

        self.assertFalse(result.ok)
        self.assertTrue(any("passa do limite de 20 %" in d for d in result.details), result.details)


class PortionTests(fixture.unittest.TestCase):
    def every(self) -> list[Slice]:
        return [Slice(f"F{n}.cs", 0, 10, 10) for n in range(10)]

    def test_the_whole_area_runs_in_the_parts_its_file_asks_for(self) -> None:
        self.assertEqual(portion(4, self.every(), self.every()), 4)

    def test_never_more_parts_than_slices(self) -> None:
        self.assertEqual(portion(48, self.every(), self.every()), 10)

    def test_the_diff_gets_its_share_of_the_parts_rounded_up(self) -> None:
        self.assertEqual(portion(10, self.every(), self.every()[:3]), 3)
        self.assertEqual(portion(4, self.every(), self.every()[:3]), 2)

    def test_a_light_diff_runs_in_one_part(self) -> None:
        self.assertEqual(portion(48, self.every(), self.every()[:1]), 1)

    def test_nothing_to_mutate_is_still_one_part(self) -> None:
        self.assertEqual(portion(4, self.every(), []), 1)
