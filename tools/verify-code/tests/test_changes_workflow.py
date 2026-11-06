"""Tests for how the workflow dispenses the heavy jobs while the required checks keep reporting."""

from __future__ import annotations

import re
import shutil
import subprocess
import unittest

from . import support

WORKFLOWS = support.VERIFY_CODE.parents[1] / ".github" / "workflows"
WORKFLOW = WORKFLOWS / "verify-code.yml"
DISPENSED = (
    "dotnet",
    "python",
    "properties",
    "package",
    "offline_start",
    "service_folder",
    "mutation_modules",
    "ruler_proof",
)
ALWAYS = (
    "dependencies",
    "secrets",
    "trivy",
    "static_analysis",
    "pr_size",
    "ai_attribution",
    "changes",
)
HEAVY_ONLY = "needs.changes.outputs.heavy != 'false'"
NOT_CANCELLED = "!cancelled()"
BASH = shutil.which("bash")


def job(name: str) -> str:
    return support.workflow_job(WORKFLOW.read_text(encoding="utf-8"), name)


def aggregator_script() -> str:
    """The `run:` block of the mutation aggregator, as bash runs it."""
    text = job("mutation")
    block = text[text.index("        run: |\n") + len("        run: |\n") :]
    return "\n".join(line[10:] for line in block.splitlines())


def aggregate(classification: str, heavy: str, modules: str, matrix: str) -> int:
    """The exit code of the aggregator for the results GitHub would hand it."""
    assert BASH is not None
    done = subprocess.run(
        [BASH, "-c", aggregator_script()],
        env={
            "CLASSIFICATION": classification,
            "HEAVY": heavy,
            "RESULTS": f"{modules} {matrix}",
            "PATH": "/usr/bin:/bin",
        },
        capture_output=True,
        text=True,
        check=False,
    )
    return done.returncode


class ChangesWorkflowTests(unittest.TestCase):
    def test_the_classification_asks_for_the_diff_of_the_pull_request_only(self) -> None:
        text = job("changes")

        self.assertIn("fetch-depth: 0", text)
        self.assertIn("verify.py --changes", text)
        self.assertIn(
            "github.event_name == 'pull_request' && format('--base origin/{0}', github.base_ref)",
            text,
        )
        self.assertIn('>> "$GITHUB_OUTPUT"', text)

    def test_the_heavy_jobs_wait_for_the_classification_and_run_unless_it_dispenses_them(
        self,
    ) -> None:
        for name in DISPENSED:
            with self.subTest(job=name):
                text = job(name)
                self.assertIn("needs: changes", text)
                condition = next(line for line in text.splitlines() if line.startswith("    if:"))
                self.assertIn(HEAVY_ONLY, condition)
                self.assertIn(NOT_CANCELLED, condition)

    def test_a_failed_classification_runs_the_heavy_jobs_instead_of_skipping_them(self) -> None:
        for name in DISPENSED:
            with self.subTest(job=name):
                condition = next(
                    line for line in job(name).splitlines() if line.startswith("    if:")
                )
                self.assertNotIn("needs.changes.result == 'success'", condition)
                self.assertNotIn("heavy == 'true'", condition)

    def test_the_schedule_still_skips_what_it_always_skipped(self) -> None:
        for name in set(DISPENSED) - {"offline_start"}:
            with self.subTest(job=name):
                self.assertIn("github.event_name != 'schedule'", job(name))

    def test_the_light_jobs_never_wait_for_the_classification_nor_depend_on_it(self) -> None:
        for name in ALWAYS:
            with self.subTest(job=name):
                self.assertNotIn("needs.changes", job(name))
        for name in set(ALWAYS) - {"changes"}:
            with self.subTest(job=name):
                self.assertNotIn("needs: changes", job(name))

    def test_the_aggregator_always_reports_and_looks_at_the_classification(self) -> None:
        text = job("mutation")

        self.assertIn("needs: [changes, mutation_modules, mutation_module]", text)
        self.assertIn("if: always() && github.event_name != 'schedule'", text)
        self.assertIn("name: Mutação nos módulos críticos", text)

    def test_no_workflow_filters_by_path_because_that_leaves_the_required_check_pending(
        self,
    ) -> None:
        for path in WORKFLOWS.glob("*.yml"):
            with self.subTest(workflow=path.name):
                self.assertIsNone(
                    re.search(r"^\s+paths(-ignore)?:", path.read_text(encoding="utf-8"), re.M)
                )

    def test_the_documentation_and_codeql_checks_still_run_on_every_pull_request(self) -> None:
        for name in ("verify-docs.yml", "codeql.yml"):
            with self.subTest(workflow=name):
                text = (WORKFLOWS / name).read_text(encoding="utf-8")
                self.assertNotIn("needs.changes", text)
                self.assertNotIn("heavy", text)


@unittest.skipIf(BASH is None, "bash is needed to run the aggregator as the runner does")
class AggregatorTests(unittest.TestCase):
    def test_a_dispensed_matrix_passes_when_the_classification_dispensed_it(self) -> None:
        self.assertEqual(aggregate("success", "false", "skipped", "skipped"), 0)

    def test_a_green_matrix_passes_when_the_classification_asked_for_it(self) -> None:
        self.assertEqual(aggregate("success", "true", "success", "success"), 0)

    def test_a_matrix_that_ran_and_failed_reproves(self) -> None:
        self.assertEqual(aggregate("success", "true", "success", "failure"), 1)

    def test_a_matrix_that_was_cancelled_reproves(self) -> None:
        self.assertEqual(aggregate("success", "true", "success", "cancelled"), 1)

    def test_a_skipped_matrix_reproves_when_the_classification_asked_for_the_ruler(self) -> None:
        self.assertEqual(aggregate("success", "true", "skipped", "skipped"), 1)

    def test_a_skipped_matrix_reproves_when_the_classification_failed(self) -> None:
        self.assertEqual(aggregate("failure", "", "skipped", "skipped"), 1)
        self.assertEqual(aggregate("failure", "false", "skipped", "skipped"), 1)

    def test_a_dispensed_classification_does_not_accept_a_matrix_that_ran(self) -> None:
        self.assertEqual(aggregate("success", "false", "success", "success"), 1)

    def test_a_green_matrix_after_a_failed_classification_passes(self) -> None:
        self.assertEqual(aggregate("failure", "", "success", "success"), 0)

    def test_the_list_failing_reproves_even_if_the_matrix_is_skipped(self) -> None:
        self.assertEqual(aggregate("success", "true", "failure", "skipped"), 1)


if __name__ == "__main__":
    unittest.main()
