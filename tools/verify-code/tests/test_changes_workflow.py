"""Tests for how the workflow dispenses the heavy jobs while the required checks keep reporting."""

from __future__ import annotations

import re
import shutil
import subprocess
import unittest

from steps import tested_tree

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
        env, run = text[text.index("      - id: classify") :].split("        run:", 1)
        self.assertIn("format('origin/{0}', github.base_ref)", env)
        self.assertIn("BASE: ${{ github.event_name == 'pull_request' &&", env)
        self.assertIn('${BASE:+--base "$BASE"}', run)
        self.assertNotIn("${{", run)
        self.assertIn('>> "$GITHUB_OUTPUT"', text)

    def test_only_a_push_asks_whether_a_pull_request_tested_the_tree(self) -> None:
        text = job("changes")

        self.assertIn("TESTED_SHA: ${{ github.event_name == 'push' && github.sha || '' }}", text)
        self.assertIn('${TESTED_SHA:+--tested-sha "$TESTED_SHA" --repository', text)
        for permission in ("contents: read", "actions: read", "checks: read"):
            self.assertIn(f"      {permission}\n", text)
        for output in ("heavy", "tested_run", "tested_head", "promote"):
            self.assertIn(f"{output}: ${{{{ steps.classify.outputs.{output} }}}}", text)

    def test_a_pull_request_run_records_the_tree_of_the_merge_ref_it_checked_out(self) -> None:
        text = job("changes")
        record = text[text.index("      - id: tree") : text.index("      - id: classify")]

        self.assertIn("if: github.event_name == 'pull_request'", record)
        self.assertIn("git rev-parse 'HEAD^{tree}'", record)
        self.assertIn("name: arvore-${{ steps.tree.outputs.tree }}", record)
        self.assertIn("overwrite: true", record)
        self.assertIn(
            tested_tree.TREE_ARTIFACT.format(tree="${{ steps.tree.outputs.tree }}"), record
        )
        self.assertLess(text.index("      - id: tree"), text.index("      - id: classify"))

    def test_the_promotion_runs_on_a_push_with_a_tested_tree_and_only_for_the_tables_it_has(
        self,
    ) -> None:
        text = job("promote_coverage")

        self.assertIn(
            "if: ${{ github.event_name == 'push' && needs.changes.outputs.tested_run", text
        )
        steps = re.split(r"^      - ", text, flags=re.MULTILINE)[1:]
        self.assertEqual(len(steps), 3)
        for step in steps:
            self.assertIn("if: contains(needs.changes.outputs.promote, matrix.language)", step)
        self.assertIn("key: cobertura-${{ matrix.language }}-${{ github.sha }}", text)
        self.assertIn("path: ${{ runner.temp }}/cobertura-main\n", text)
        self.assertNotIn('run: gh run download "${{', text)

    def test_each_job_that_measures_the_coverage_keeps_the_table_for_the_promotion(self) -> None:
        for name in ("dotnet", "python"):
            with self.subTest(job=name):
                text = job(name)
                upload = text[text.index("Guardar o by-area.md do pull request") :]
                upload = upload[: upload.index("      - name: Separar")]

                self.assertIn("if: github.event_name == 'pull_request'", upload)
                head = "${{ github.event.pull_request.head.sha }}"
                expected = tested_tree.ARTIFACT.format(language=name, head=head)
                self.assertIn(f"name: {expected}", upload)
                self.assertIn("if-no-files-found: error", upload)
                self.assertIn("path: .verify-code/coverage/by-area.md", upload)

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
