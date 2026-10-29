"""Tests for the workflow jobs of the mutation step: one job per critical module, a ceiling for each
and an aggregate under the name the check always had.

Leaving the block is a written decision (decisão 0007), never a line of the workflow.
"""

from __future__ import annotations

import re
import unittest

from steps import areas_doc, mutation_policy
from steps.mutation_policy import MutationPolicy

from . import support
from .test_secrets_configuration import WORKFLOW, job_of

ROOT = support.VERIFY_CODE.parents[1]
TIMEOUT = re.compile(r"^    timeout-minutes: (\d+)$", re.MULTILINE)
AGGREGATE_NAME = "name: Mutação nos módulos críticos\n"
MATRIX = "module: ${{ fromJSON(needs.mutation_modules.outputs.modules) }}"
LISTING = "modules=$(python tools/verify-code/verify.py --mutation-modules)\n"


def text() -> str:
    return WORKFLOW.read_text(encoding="utf-8")


def module_job() -> str:
    return job_of(text(), "mutation_module")


def aggregate_job() -> str:
    return job_of(text(), "mutation")


def limit_minutes() -> int:
    found = TIMEOUT.search(module_job())
    assert found is not None
    return int(found.group(1))


def policy() -> MutationPolicy:
    found = mutation_policy.read_policy(ROOT)
    assert isinstance(found, MutationPolicy)
    return found


class MutationModuleJobTests(unittest.TestCase):
    def test_the_job_never_continues_on_error(self) -> None:
        self.assertNotIn("continue-on-error", module_job())

    def test_the_job_runs_the_mutation_step_for_the_module_of_the_matrix(self) -> None:
        self.assertIn('--step mutation --module "$MODULE"', module_job())
        self.assertIn("MODULE: ${{ matrix.module }}", module_job())

    def test_the_job_runs_on_pull_requests(self) -> None:
        self.assertIn("if: github.event_name != 'schedule'\n", module_job())

    def test_one_failing_module_does_not_cancel_the_others(self) -> None:
        self.assertIn("fail-fast: false", module_job())

    def test_the_matrix_comes_from_the_job_that_reads_the_document(self) -> None:
        self.assertIn(MATRIX, module_job())
        self.assertIn("needs: mutation_modules\n", module_job())

    def test_the_job_limit_outlasts_the_ceiling_of_one_module(self) -> None:
        self.assertGreater(limit_minutes(), policy().minutes)

    def test_the_job_limit_does_not_hold_two_ceilings(self) -> None:
        self.assertLessEqual(limit_minutes(), 2 * policy().minutes)

    def test_the_job_installs_the_uv_the_service_side_needs(self) -> None:
        requirements = "pip install -r tools/verify-code/service-requirements.txt"

        self.assertIn(requirements, module_job())

    def test_no_module_is_named_in_the_workflow(self) -> None:
        names = (area.prefix for area in areas_doc.read_areas(ROOT).areas if area.critical)

        self.assertEqual([name for name in names if name in text()], [])


class MutationModulesJobTests(unittest.TestCase):
    def test_the_list_is_read_from_the_step_that_reads_the_document(self) -> None:
        job = job_of(text(), "mutation_modules")

        self.assertIn("modules: ${{ steps.list.outputs.modules }}", job)
        self.assertIn(LISTING, job)

    def test_the_listing_is_not_piped_so_its_failure_fails_the_job(self) -> None:
        job = job_of(text(), "mutation_modules")

        self.assertNotIn('echo "modules=$(', job)


class MutationAggregateJobTests(unittest.TestCase):
    def test_the_aggregate_keeps_the_name_the_check_always_had(self) -> None:
        self.assertIn(AGGREGATE_NAME, aggregate_job())

    def test_no_other_job_takes_the_name_of_the_aggregate(self) -> None:
        self.assertEqual(text().count(AGGREGATE_NAME), 1)

    def test_the_aggregate_waits_for_the_list_and_for_every_module(self) -> None:
        self.assertIn("needs: [mutation_modules, mutation_module]\n", aggregate_job())

    def test_the_aggregate_runs_when_a_module_fails_but_not_on_the_schedule(self) -> None:
        self.assertIn("if: always() && github.event_name != 'schedule'\n", aggregate_job())

    def test_the_aggregate_fails_unless_every_result_is_success(self) -> None:
        job = aggregate_job()

        self.assertIn("RESULTS: ${{ join(needs.*.result, ' ') }}", job)
        self.assertIn('[ "$result" = success ] || exit 1', job)

    def test_the_aggregate_never_continues_on_error(self) -> None:
        self.assertNotIn("continue-on-error", aggregate_job())
