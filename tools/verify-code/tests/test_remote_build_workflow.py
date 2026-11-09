"""Tests for the workflow that builds a branch on a runner, on demand, and not as a PR check."""

from __future__ import annotations

import unittest
from typing import Any, cast

import yaml

from . import support

WORKFLOW = support.VERIFY_CODE.parents[1] / ".github" / "workflows" / "build-remoto.yml"


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
        runs = "\n".join(str(step.get("run", "")) for step in self.steps)

        self.assertNotIn("${{", runs)

    def test_does_what_the_local_build_does(self) -> None:
        run = "\n".join(str(step.get("run", "")) for step in self.steps)

        for command in (
            "dotnet build core/Core.slnx -c Release",
            "dotnet format core/Core.slnx --verify-no-changes",
            "dotnet test --project core/tests/Core.Tests.Architecture/Core.Tests.Architecture.csproj",
        ):
            with self.subTest(command=command):
                self.assertIn(command, run)

    def test_fails_unless_the_build_and_the_format_pass(self) -> None:
        run = "\n".join(str(step.get("run", "")) for step in self.steps)

        self.assertIn('[ "$build" = 0 ] && [ "$format" = 0 ]', run)


if __name__ == "__main__":
    unittest.main()
