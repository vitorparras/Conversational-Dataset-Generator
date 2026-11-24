"""Tests for the mutation of the diff in the remote build: asked for, it runs the list and the step
the pull request runs, against main, on the branch it received."""

from __future__ import annotations

import unittest
from typing import Any, cast

import yaml

from . import support

WORKFLOWS = support.VERIFY_CODE.parents[1] / ".github" / "workflows"
REMOTE = WORKFLOWS / "build-remoto.yml"
PULL_REQUEST = WORKFLOWS / "verify-code.yml"
JOBS = ("mutation_modules", "mutation_module")
SYNC = "retry.py -- uv sync --locked --directory language-service"


def jobs(path: Any) -> dict[str, Any]:
    return cast("dict[str, Any]", yaml.safe_load(path.read_text(encoding="utf-8"))["jobs"])


def runs(job: dict[str, Any]) -> list[str]:
    return [str(step.get("run", "")) for step in job["steps"]]


class RemoteMutationTests(unittest.TestCase):
    def setUp(self) -> None:
        self.remote = jobs(REMOTE)

    def test_the_mutation_is_an_option_that_defaults_to_off(self) -> None:
        inputs = yaml.safe_load(REMOTE.read_text(encoding="utf-8"))[True]["workflow_dispatch"]
        option = inputs["inputs"]["mutation"]

        self.assertEqual((option["type"], option["default"]), ("boolean", False))
        self.assertFalse(option["required"])

    def test_the_jobs_run_only_when_asked(self) -> None:
        for name in JOBS:
            with self.subTest(job=name):
                self.assertIn("inputs.mutation", self.remote[name]["if"])

    def test_each_job_checks_out_the_branch_with_main_and_no_credential(self) -> None:
        for name in JOBS:
            with self.subTest(job=name):
                checkout = self.remote[name]["steps"][0]["with"]
                self.assertEqual(checkout["ref"], "${{ inputs.branch }}")
                self.assertIs(checkout["persist-credentials"], False)
                self.assertEqual(checkout["fetch-depth"], 0)

    def test_the_list_is_the_one_of_the_pull_request_against_main(self) -> None:
        self.assertIn(
            "verify.py --mutation-modules --base origin/main", "\n".join(runs(self.remote[JOBS[0]]))
        )

    def test_the_matrix_runs_the_step_of_the_pull_request_against_main(self) -> None:
        step = '--step mutation --module "$MODULE"'
        remote = "\n".join(runs(self.remote[JOBS[1]]))
        pull_request = "\n".join(runs(jobs(PULL_REQUEST)[JOBS[1]]))

        self.assertIn(f"{step} --base origin/main", remote)
        self.assertIn(step, pull_request)
        self.assertIn(SYNC, remote)

    def test_the_matrix_has_the_ceiling_and_the_isolation_of_the_pull_request(self) -> None:
        remote, pull_request = self.remote[JOBS[1]], jobs(PULL_REQUEST)[JOBS[1]]

        self.assertEqual(remote["timeout-minutes"], pull_request["timeout-minutes"])
        self.assertIs(remote["strategy"]["fail-fast"], False)
        self.assertEqual(remote["strategy"]["matrix"], pull_request["strategy"]["matrix"])
        self.assertIn("needs.mutation_modules.outputs.modules != '[]'", remote["if"])

    def test_no_branch_enters_the_shell_text(self) -> None:
        for name in JOBS:
            with self.subTest(job=name):
                self.assertNotIn("${{", "\n".join(runs(self.remote[name])))


if __name__ == "__main__":
    unittest.main()
