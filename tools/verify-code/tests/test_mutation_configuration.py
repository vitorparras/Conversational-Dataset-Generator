"""Tests for the workflow job of the mutation step: it blocks, and it outlasts both ceilings.

Leaving the block is a written decision (decisão 0007), never a line of the workflow.
"""

from __future__ import annotations

import re
import unittest

from steps import mutation_policy
from steps.mutation_policy import MutationPolicy

from . import support
from .test_secrets_configuration import WORKFLOW, job_of

ROOT = support.VERIFY_CODE.parents[1]
TIMEOUT = re.compile(r"^    timeout-minutes: (\d+)$", re.MULTILINE)
LANGUAGES = 2


def mutation_job() -> str:
    return job_of(WORKFLOW.read_text(encoding="utf-8"), "mutation")


def limit_minutes() -> int:
    found = TIMEOUT.search(mutation_job())
    assert found is not None
    return int(found.group(1))


def policy() -> MutationPolicy:
    found = mutation_policy.read_policy(ROOT)
    assert isinstance(found, MutationPolicy)
    return found


class MutationJobTests(unittest.TestCase):
    def test_the_job_never_continues_on_error(self) -> None:
        self.assertNotIn("continue-on-error", mutation_job())

    def test_the_job_runs_the_mutation_step(self) -> None:
        self.assertIn("run: python tools/verify-code/verify.py --step mutation\n", mutation_job())

    def test_the_job_runs_on_pull_requests(self) -> None:
        self.assertIn("if: github.event_name != 'schedule'\n", mutation_job())

    def test_the_job_has_a_time_limit(self) -> None:
        self.assertIsNotNone(TIMEOUT.search(mutation_job()))

    def test_the_job_limit_outlasts_the_ceiling_of_both_languages(self) -> None:
        self.assertGreater(limit_minutes(), LANGUAGES * policy().minutes)

    def test_the_job_installs_the_uv_the_service_side_needs(self) -> None:
        requirements = "pip install -r tools/verify-code/service-requirements.txt"

        self.assertIn(requirements, mutation_job())
