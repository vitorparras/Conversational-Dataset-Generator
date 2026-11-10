"""Tests for the workflow that builds a branch on a runner, on demand, and not as a PR check."""

from __future__ import annotations

import shutil
import subprocess
import tempfile
import unittest
from collections.abc import Iterator
from contextlib import contextmanager
from pathlib import Path
from typing import Any, cast

import yaml

from . import support

WORKFLOW = support.VERIFY_CODE.parents[1] / ".github" / "workflows" / "build-remoto.yml"
BASH = shutil.which("bash")
SOLUTION = (
    '<Solution><Project Path="tests/Core.Host.Tests/Core.Host.Tests.csproj" />'
    '<Project Path="tests/Core.Ingestion.Tests/Core.Ingestion.Tests.csproj" /></Solution>'
)
PROJECTS = (("Core.Host.Tests", "Core.Tests.Database"), ("Core.Ingestion.Tests", "Core.Tests.Support"))


@contextmanager
def workspace() -> Iterator[Path]:
    """A working copy with the solution and two test projects, only the first referencing the database."""
    with tempfile.TemporaryDirectory() as folder:
        root = Path(folder)
        (root / "core").mkdir()
        (root / "core" / "Core.slnx").write_text(SOLUTION, encoding="utf-8")
        for name, reference in PROJECTS:
            project = root / "core" / "tests" / name
            project.mkdir(parents=True)
            (project / f"{name}.csproj").write_text(
                f'<Project><ItemGroup><ProjectReference Include="..\\{reference}\\{reference}.csproj" /></ItemGroup></Project>',
                encoding="utf-8",
            )
        yield root


class RemoteBuildWorkflowTests(unittest.TestCase):
    def setUp(self) -> None:
        self.text = WORKFLOW.read_text(encoding="utf-8")
        self.document = cast("dict[Any, Any]", yaml.safe_load(self.text))
        self.steps = cast("list[dict[str, Any]]", self.document["jobs"]["build"]["steps"])

    def test_runs_only_when_asked_so_it_is_no_check_of_a_pull_request(self) -> None:
        # PyYAML reads the key `on` as True.
        self.assertEqual(list(self.document[True]), ["workflow_dispatch"])

    def test_asks_for_the_branch_and_for_the_architecture_as_an_option(self) -> None:
        inputs = self.document[True]["workflow_dispatch"]["inputs"]

        self.assertTrue(inputs["branch"]["required"])
        self.assertFalse(inputs["architecture"]["default"])

    def test_checks_out_the_branch_it_received_and_keeps_no_credential(self) -> None:
        checkout = self.steps[0]["with"]

        self.assertEqual(checkout["ref"], "${{ inputs.branch }}")
        self.assertIs(checkout["persist-credentials"], False)

    def test_writes_nothing_to_the_repository(self) -> None:
        self.assertEqual(self.document["permissions"], {"contents": "read"})

    def test_the_branch_never_enters_the_shell_text(self) -> None:
        self.assertNotIn("${{", self.runs())

    def test_does_what_the_local_build_does(self) -> None:
        run = self.runs()

        for command in (
            "dotnet build core/Core.slnx -c Release",
            "dotnet format core/Core.slnx --verify-no-changes",
            "dotnet test --project "
            "core/tests/Core.Tests.Architecture/Core.Tests.Architecture.csproj",
        ):
            with self.subTest(command=command):
                self.assertIn(command, run)

    def test_fails_unless_the_build_and_the_format_pass(self) -> None:
        self.assertIn('[ "$build" = 0 ] && [ "$format" = 0 ]', self.runs())

    def test_asks_for_the_test_projects_as_an_optional_list_that_defaults_to_none(self) -> None:
        tests = self.document[True]["workflow_dispatch"]["inputs"]["tests"]

        self.assertFalse(tests["required"])
        self.assertEqual(tests["default"], "")
        self.assertEqual(tests["type"], "string")

    def test_runs_each_listed_project_on_the_build_it_made_and_without_the_real_service(self) -> None:
        run = self.runs()

        self.assertIn('dotnet test --project "core/tests/$project/$project.csproj" -c Release --no-build', run)
        self.assertIn('--filter-not-trait "Category=RealService"', run)

    def test_the_summary_line_gains_tests_only_when_some_were_asked_for(self) -> None:
        run = self.runs()

        self.assertIn('[ "$tests" = skipped ] || line="$line tests=$tests"', run)
        self.assertIn("tests=skipped", run)

    def test_fails_unless_the_listed_tests_pass_too(self) -> None:
        self.assertIn('{ [ "$tests" = skipped ] || [ "$tests" = 0 ]; }', self.runs())

    def test_builds_the_database_image_only_for_a_plan_that_needs_it(self) -> None:
        image = next(s for s in self.steps if s.get("uses") == "./.github/actions/build-database-image")

        self.assertEqual(image["if"], "steps.plan.outputs.database == 'true'")

    def test_the_list_reaches_the_shell_through_the_environment_and_not_through_the_text(self) -> None:
        plan = next(step for step in self.steps if step.get("id") == "plan")

        self.assertEqual(plan["env"], {"TESTS": "${{ inputs.tests }}"})
        self.assertNotIn("${{", str(plan["run"]))

    def runs(self) -> str:
        return "\n".join(str(step.get("run", "")) for step in self.steps)


@unittest.skipIf(BASH is None, "bash is needed to run the plan as the runner does")
class PlanStepTests(unittest.TestCase):
    def setUp(self) -> None:
        document = cast("dict[Any, Any]", yaml.safe_load(WORKFLOW.read_text(encoding="utf-8")))
        steps = cast("list[dict[str, Any]]", document["jobs"]["build"]["steps"])
        self.script = str(next(step for step in steps if step.get("id") == "plan")["run"])

    def plan(self, tests: str, root: Path) -> subprocess.CompletedProcess[str]:
        assert BASH is not None
        env = {
            "TESTS": tests,
            "GITHUB_OUTPUT": (root / "output").as_posix(),
            "GITHUB_STEP_SUMMARY": (root / "summary").as_posix(),
            "PATH": "/usr/bin:/bin",
        }
        return subprocess.run(
            [BASH, "-c", self.script],
            cwd=root,
            env=env,
            capture_output=True,
            text=True,
            encoding="utf-8",
            check=False,
        )

    @staticmethod
    def outputs(root: Path) -> dict[str, str]:
        lines = (root / "output").read_text(encoding="utf-8").splitlines()
        return dict(line.split("=", 1) for line in lines)

    def test_names_the_projects_and_says_the_database_is_needed_when_one_references_it(self) -> None:
        with workspace() as root:
            done = self.plan("Core.Host.Tests; Core.Ingestion.Tests;", root)

            self.assertEqual(done.returncode, 0, done.stdout + done.stderr)
            self.assertEqual(
                self.outputs(root),
                {"database": "true", "projects": "Core.Host.Tests;Core.Ingestion.Tests;"},
            )

    def test_does_not_ask_for_the_database_when_no_project_references_it(self) -> None:
        with workspace() as root:
            done = self.plan("Core.Ingestion.Tests", root)

            self.assertEqual(done.returncode, 0, done.stdout + done.stderr)
            self.assertEqual(self.outputs(root), {"database": "false", "projects": "Core.Ingestion.Tests;"})

    def test_an_empty_list_asks_for_nothing(self) -> None:
        with workspace() as root:
            done = self.plan("", root)

            self.assertEqual(done.returncode, 0, done.stdout + done.stderr)
            self.assertEqual(self.outputs(root), {"database": "false", "projects": ""})

    def test_a_name_that_is_not_a_project_of_the_solution_fails_before_anything_is_built(self) -> None:
        for name in ("Core.Nao.Existe", "Core.Host.Tests.Browser", "../x", "A B"):
            with self.subTest(name=name), workspace() as root:
                done = self.plan(f"Core.Ingestion.Tests;{name}", root)

                self.assertNotEqual(done.returncode, 0)
                self.assertIn(f"error: '{name}'", done.stdout)
                self.assertIn("Core.slnx", done.stdout)


if __name__ == "__main__":
    unittest.main()
