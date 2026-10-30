"""Tests for how the workflow keeps the main's by-area.md and hands it to the pull request."""

from __future__ import annotations

import re
import unittest

from steps import coverage_by_area

from . import support

WORKFLOW = support.VERIFY_CODE.parents[1] / ".github" / "workflows" / "verify-code.yml"
JOBS = ("dotnet", "python")
CACHE = "actions/cache@"
LONG_RETENTION_DAYS = 90
TABLE = f".verify-code/{coverage_by_area.RESULTS}/{coverage_by_area.TABLE}"


def step_of(job: str, marker: str) -> str:
    """The text of the step of ``job`` that contains ``marker``, up to the next step."""
    steps = re.split(r"^      - ", job, flags=re.MULTILINE)
    return next(step for step in steps if marker in step)


def verify_run(job: str) -> str:
    """The step of ``job`` that calls verify.py with the floors step."""
    return step_of(job, "floors")


class CoverageBaselineWorkflowTests(unittest.TestCase):
    def setUp(self) -> None:
        text = WORKFLOW.read_text(encoding="utf-8")
        self.jobs = {name: support.workflow_job(text, name) for name in JOBS}

    def test_each_job_keeps_the_table_it_measured_on_a_push_to_main(self) -> None:
        for name, job in self.jobs.items():
            with self.subTest(job=name):
                save = step_of(job, "actions/cache/save@")
                self.assertIn("github.event_name == 'push'", save)
                self.assertIn(f"key: cobertura-{name}-${{{{ github.sha }}}}", save)

    def test_each_job_also_uploads_the_table_as_an_artifact_with_long_retention(self) -> None:
        for name, job in self.jobs.items():
            with self.subTest(job=name):
                upload = step_of(job, "actions/upload-artifact@")
                self.assertIn("github.event_name == 'push'", upload)
                self.assertIn(f"name: cobertura-{name}-${{{{ github.sha }}}}", upload)
                self.assertIn(f"path: {TABLE}", upload)
                self.assertIn(f"retention-days: {LONG_RETENTION_DAYS}", upload)
                self.assertIn("if-no-files-found: error", upload)

    def test_the_cached_copy_is_the_table_the_coverage_step_wrote(self) -> None:
        for name, job in self.jobs.items():
            with self.subTest(job=name):
                copy = step_of(job, f"cp {TABLE}")
                self.assertIn("github.event_name == 'push'", copy)
                self.assertIn("$RUNNER_TEMP/cobertura-main/by-area.md", copy)

    def test_the_pull_request_restores_the_latest_table_of_the_job_from_the_main(self) -> None:
        for name, job in self.jobs.items():
            with self.subTest(job=name):
                restore = step_of(job, "actions/cache/restore@")
                self.assertIn("github.event_name == 'pull_request'", restore)
                self.assertIn(f"restore-keys: cobertura-{name}-\n", restore)
                self.assertIn("path: ${{ runner.temp }}/cobertura-main", restore)

    def test_the_restore_comes_before_the_floors_and_the_save_after_it(self) -> None:
        for name, job in self.jobs.items():
            with self.subTest(job=name):
                restore, floors = job.index("actions/cache/restore@"), job.index("floors")
                self.assertLess(restore, floors)
                self.assertLess(floors, job.index("actions/cache/save@"))

    def test_floors_gets_previous_only_when_the_main_left_a_table(self) -> None:
        for name, job in self.jobs.items():
            with self.subTest(job=name):
                run = verify_run(job)
                self.assertIn('if [ -f "$RUNNER_TEMP/cobertura-main/by-area.md" ]; then', run)
                self.assertIn('previous=(--previous "$RUNNER_TEMP/cobertura-main/by-area.md")', run)
                self.assertIn('verify.py "${previous[@]}" --step', run)

    def test_the_save_and_the_upload_run_only_after_the_job_steps_passed(self) -> None:
        for name, job in self.jobs.items():
            with self.subTest(job=name):
                for marker in ("actions/cache/save@", "actions/upload-artifact@"):
                    self.assertNotIn("always()", step_of(job, marker))

    def test_the_cache_actions_are_pinned_to_the_commit_the_trivy_cache_uses(self) -> None:
        text = WORKFLOW.read_text(encoding="utf-8")
        pinned = re.search(rf"{CACHE}(\w{{40}}) ", text)
        assert pinned is not None

        for sub in ("restore", "save"):
            self.assertIn(f"actions/cache/{sub}@{pinned[1]} ", text)


if __name__ == "__main__":
    unittest.main()
