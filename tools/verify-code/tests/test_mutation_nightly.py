"""Tests for the nightly mutation of main: the schedule runs every day, the matrix keeps the report
of a module that fails, and a failure opens one issue per module, never two while one is open."""

from __future__ import annotations

import json
import tempfile
import unittest
from collections.abc import Sequence
from dataclasses import dataclass, field
from pathlib import Path

import mutation_issue
import yaml

from steps.common import CommandOutcome

from . import support

WORKFLOW = support.VERIFY_CODE.parents[1] / ".github/workflows/verify-code.yml"
RUN = "https://example.invalid/runs/1"
SURVIVOR = "  sobrevivente: core/src/Core.Alpha/Code.cs:12 — troca de + por -"
REPORT = f"FALHOU  mutation       3.0 s\n       Alpha (`Core.Alpha`): 4 gerados\n{SURVIVOR}\n"
TITLE = "Mutação noturna reprovou em `Core.Alpha`"
TITLE_UNREPORTED = "Mutação noturna reprovou em `sem relatório`"


def job(name: str) -> str:
    return support.workflow_job(WORKFLOW.read_text(encoding="utf-8"), name)


@dataclass
class FakeGitHub:
    """Answers `gh` with the open issues it holds and records what was asked of it."""

    open_issues: list[dict[str, object]] = field(default_factory=list[dict[str, object]])
    listing_fails: bool = False
    calls: list[list[str]] = field(default_factory=list[list[str]])
    bodies: list[str] = field(default_factory=list[str])

    def __call__(self, command: Sequence[str], cwd: Path, seconds: float = 0.0) -> CommandOutcome:
        self.calls.append(list(command))
        if "--body-file" in command:
            self.bodies.append(Path(command[command.index("--body-file") + 1]).read_text("utf-8"))
        if command[1:3] == ["issue", "list"]:
            return CommandOutcome(not self.listing_fails, json.dumps(self.open_issues))
        return CommandOutcome(True, "")

    def asked(self, verb: str) -> list[list[str]]:
        return [call for call in self.calls if call[1:3] == ["issue", verb]]


class IssueTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.folder = Path(self._tmp.name)

    def failed(self, module: str, report: str = REPORT) -> None:
        artifact = self.folder / f"relatorio-{module.replace('/', '-').replace('@', '-')}"
        support.write(artifact, "modulo.txt", f"{module}\n")
        support.write(artifact, "relatorio.txt", report)

    def file(self, github: FakeGitHub) -> int:
        return mutation_issue.main([str(self.folder), RUN], run=github)

    def test_a_failure_opens_an_issue_with_the_module_the_run_and_the_survivors(self) -> None:
        self.failed("Core.Alpha")
        github = FakeGitHub()

        code = self.file(github)

        self.assertEqual(code, 0)
        created = github.asked("create")
        self.assertEqual(len(created), 1)
        self.assertEqual(created[0][created[0].index("--title") + 1], TITLE)
        self.assertIn(RUN, github.bodies[0])
        self.assertIn("`Core.Alpha`", github.bodies[0])
        self.assertIn(SURVIVOR.strip(), github.bodies[0])

    def test_a_second_failure_of_the_module_comments_on_the_open_issue(self) -> None:
        self.failed("Core.Alpha")
        github = FakeGitHub(open_issues=[{"number": 7, "title": TITLE}])

        self.file(github)

        self.assertEqual(github.asked("create"), [])
        self.assertEqual([call[3] for call in github.asked("comment")], ["7"])
        self.assertIn(RUN, github.bodies[0])

    def test_an_open_issue_of_another_module_does_not_take_the_failure(self) -> None:
        self.failed("Core.Alpha")
        other = "Mutação noturna reprovou em `Core.Alpha.Inner`"
        github = FakeGitHub(open_issues=[{"number": 7, "title": other}])

        self.file(github)

        self.assertEqual(len(github.asked("create")), 1)
        self.assertEqual(github.asked("comment"), [])

    def test_the_parts_of_one_module_make_one_issue(self) -> None:
        self.failed("Core.Alpha@1/2")
        self.failed("Core.Alpha@2/2")
        github = FakeGitHub()

        self.file(github)

        self.assertEqual(len(github.asked("create")), 1)
        self.assertIn("Core.Alpha@1/2", github.bodies[0])
        self.assertIn("Core.Alpha@2/2", github.bodies[0])

    def test_two_modules_make_two_issues(self) -> None:
        self.failed("Core.Alpha")
        self.failed("Core.Beta", REPORT.replace("Alpha", "Beta"))
        github = FakeGitHub()

        self.file(github)

        titles = [call[call.index("--title") + 1] for call in github.asked("create")]
        self.assertEqual(sorted(titles), [TITLE, TITLE.replace("Alpha", "Beta")])

    def test_a_failure_with_no_survivor_says_where_to_look(self) -> None:
        self.failed("Core.Alpha", "FALHOU  mutation\n       teto de 20 min excedido\n")
        github = FakeGitHub()

        self.file(github)

        self.assertIn("nenhum sobrevivente listado", github.bodies[0])
        self.assertIn("teto de 20 min excedido", github.bodies[0])

    def test_a_listing_that_fails_opens_nothing_and_fails(self) -> None:
        self.failed("Core.Alpha")
        github = FakeGitHub(listing_fails=True)

        code = self.file(github)

        self.assertEqual(code, 1)
        self.assertEqual(github.asked("create"), [])

    def test_a_failure_that_left_no_report_files_the_run_and_fails(self) -> None:
        github = FakeGitHub()

        code = self.file(github)

        self.assertEqual(code, 1)
        created = github.asked("create")
        self.assertEqual(len(created), 1)
        self.assertEqual(created[0][created[0].index("--title") + 1], TITLE_UNREPORTED)
        self.assertIn(RUN, github.bodies[0])

    def test_a_second_failure_with_no_report_comments_on_the_open_issue(self) -> None:
        github = FakeGitHub(open_issues=[{"number": 9, "title": TITLE_UNREPORTED}])

        self.assertEqual(self.file(github), 1)
        self.assertEqual(github.asked("create"), [])
        self.assertEqual([call[3] for call in github.asked("comment")], ["9"])

    def test_a_missing_folder_is_a_failure_with_no_report(self) -> None:
        github = FakeGitHub()

        code = mutation_issue.main([str(self.folder / "ausente"), RUN], run=github)

        self.assertEqual(code, 1)
        self.assertEqual(len(github.asked("create")), 1)


class NightlyWorkflowTests(unittest.TestCase):
    def test_the_schedule_runs_every_day(self) -> None:
        triggers = yaml.safe_load(WORKFLOW.read_text(encoding="utf-8"))[True]

        self.assertEqual(triggers["schedule"], [{"cron": "17 4 * * *"}])

    def test_the_matrix_mutates_the_diff_with_the_base_of_the_list(self) -> None:
        matrix = job("mutation_module")

        self.assertIn("BASE: ${{ needs.mutation_modules.outputs.base }}", matrix)
        self.assertIn('--step mutation --module "$MODULE" ${BASE:+--base "$BASE"}', matrix)
        self.assertIn("fetch-depth: 0", matrix)
        self.assertIn("base: ${{ steps.list.outputs.base }}", job("mutation_modules"))

    def test_a_push_to_main_runs_no_mutation(self) -> None:
        lines = job("mutation_modules").splitlines()
        condition = next(line for line in lines if line.startswith("    if:"))

        self.assertIn("github.event_name != 'push'", condition)

    def test_the_matrix_fails_when_the_step_fails_even_through_the_copy_of_its_report(self) -> None:
        self.assertIn("set -o pipefail", job("mutation_module"))

    def test_the_matrix_keeps_the_report_of_a_failing_module(self) -> None:
        matrix = job("mutation_module")

        self.assertIn("name: relatorio-${{ steps.log.outputs.name }}", matrix)
        self.assertIn('"$MODULE" > "$RUNNER_TEMP/mutacao/modulo.txt"', matrix)

    def test_only_a_failing_schedule_files_the_issue(self) -> None:
        nightly = job("mutation_nightly")

        self.assertIn("needs: mutation_module\n", nightly)
        self.assertIn("github.event_name == 'schedule'", nightly)
        self.assertIn("needs.mutation_module.result == 'failure'", nightly)
        self.assertIn("issues: write", nightly)
        self.assertIn("pattern: relatorio-*", nightly)
        self.assertIn("continue-on-error: true", nightly)
        self.assertIn("tools/verify-code/mutation_issue.py", nightly)
