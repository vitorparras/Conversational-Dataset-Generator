"""Tests for the pure parts of the measurement script: flaky jobs, percentiles and the report."""

from __future__ import annotations

import importlib.util
import sys
import unittest
from dataclasses import replace
from typing import Any

from . import support

SPEC = importlib.util.spec_from_file_location(
    "measure_pr_time", support.VERIFY_CODE / "measure_pr_time.py"
)
assert SPEC is not None and SPEC.loader is not None
measure = importlib.util.module_from_spec(SPEC)
sys.modules["measure_pr_time"] = measure
SPEC.loader.exec_module(measure)

HEAD = "a" * 40
MERGE = "b" * 40


def job(name: str, minutes: float, conclusion: str = "success") -> Any:
    return measure.JobStat(name, minutes * 60, 30.0, conclusion, 1, HEAD)


def later(stat: Any, *, attempt: int = 1, head: str = HEAD) -> Any:
    """The same job on another attempt or on another head."""
    return replace(stat, attempt=attempt, head=head)


def pull_request(number: int, docs_only: bool, *jobs: Any) -> Any:
    pr = measure.PullRequest(number, docs_only, 3, 60.0)
    pr.jobs.extend(jobs)
    pr.heads = {HEAD, MERGE}
    pr.merge_heads = {MERGE}
    return pr


class FlakeTests(unittest.TestCase):
    def test_a_failure_that_passes_on_a_later_attempt_of_the_same_head_is_a_flake(self) -> None:
        jobs = [job("Núcleo", 10, "failure"), later(job("Núcleo", 10), attempt=2)]

        self.assertEqual(measure.find_flakes(jobs), [f"Núcleo @{HEAD[:8]}"])

    def test_a_failure_that_is_never_fixed_is_not_a_flake(self) -> None:
        self.assertEqual(measure.find_flakes([job("Núcleo", 10, "failure")]), [])

    def test_a_pass_on_another_head_does_not_excuse_the_failure(self) -> None:
        jobs = [job("Núcleo", 10, "failure"), later(job("Núcleo", 10), head=MERGE)]

        self.assertEqual(measure.find_flakes(jobs), [])


class StatisticsTests(unittest.TestCase):
    def test_the_percentile_picks_a_value_of_the_sorted_list(self) -> None:
        self.assertEqual(measure.percentile([5.0, 1.0, 3.0, 2.0, 4.0], 0.9), 5.0)
        self.assertEqual(measure.percentile([5.0, 1.0, 3.0, 2.0, 4.0], 0.0), 1.0)

    def test_the_percentile_of_nothing_is_zero(self) -> None:
        self.assertEqual(measure.percentile([], 0.9), 0.0)

    def test_the_queue_counts_first_attempts_that_ran_and_never_goes_below_zero(self) -> None:
        pr = pull_request(
            1,
            False,
            job("A", 1),
            measure.JobStat("B", 60, -120.0, "success", 1, HEAD),
            job("C", 1, "cancelled"),
            job("D", 1, "skipped"),
            later(job("E", 1), attempt=2),
        )

        self.assertEqual(pr.queue, [0.5, 0.0])

    def test_the_minutes_of_the_merge_heads_are_the_ones_of_their_jobs(self) -> None:
        pr = pull_request(1, False, job("A", 10), later(job("A", 4), head=MERGE))

        self.assertEqual((pr.minutes, pr.merge_minutes), (14.0, 4.0))

    def test_only_the_jobs_the_cut_skips_count_as_dispensable(self) -> None:
        pr = pull_request(
            1,
            True,
            job("Núcleo (.NET) e ferramentas", 10),
            job("Segredos no histórico (gitleaks)", 1),
            job("CodeQL: Análise CodeQL (csharp)", 8),
        )

        self.assertEqual(pr.dispensable_minutes, 10.0)


class ReportTests(unittest.TestCase):
    def setUp(self) -> None:
        self.prs = [
            pull_request(1, True, job("Núcleo", 10), job("Mutação (X)", 6, "cancelled")),
            pull_request(
                2, False, job("Núcleo", 20, "failure"), later(job("Núcleo", 20), attempt=2)
            ),
        ]
        self.prs[1].review_rounds = 2

    def test_the_summary_separates_documents_from_code_and_counts_the_merge_heads(self) -> None:
        text = measure.summary(self.prs)

        self.assertIn("PRs só de docs: 1 PRs", text)
        self.assertIn("PRs com código: 1 PRs", text)
        self.assertIn("heads que são merge do main: 2 de 4", text)
        self.assertIn("rodadas de revisão: 2", text)
        self.assertIn("falhas intermitentes (falhou e passou na tentativa seguinte do mesmo", text)

    def test_the_summary_survives_a_group_with_no_pull_request(self) -> None:
        self.assertIn("PRs só de docs: nenhum PR", measure.summary([self.prs[1]]))

    def test_the_job_table_orders_by_total_time_and_leaves_out_skipped_jobs(self) -> None:
        text = measure.per_job([*self.prs, pull_request(3, False, job("Pulado", 9, "skipped"))])

        lines = text.splitlines()
        self.assertTrue(lines[2].startswith("| Núcleo | 3 |"))
        self.assertIn("| Mutação (X) | 1 |", lines[3])
        self.assertNotIn("Pulado | 0", "\n".join(lines[:2]))

    def test_the_pull_request_table_has_one_row_per_pull_request(self) -> None:
        lines = measure.per_pull_request(self.prs).splitlines()

        self.assertEqual(len(lines), 2 + len(self.prs))
        self.assertIn("| #1 | docs |", lines[2])
        self.assertIn("| #2 | código |", lines[3])


if __name__ == "__main__":
    unittest.main()
