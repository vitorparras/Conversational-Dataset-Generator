"""Tests for the ai_attribution step against a synthetic git repository in a temporary directory."""

from __future__ import annotations

import re
import tempfile
import unittest
from pathlib import Path

from steps import ai_attribution
from steps.common import StepResult

from . import support

WORKFLOW = support.VERIFY_CODE.parents[1] / ".github" / "workflows" / "verify-code.yml"
ASSISTANT_TRAILER = "Co-authored-by: Claude <noreply@anthropic.com>"
GENERATED_LINE = "🤖 Generated with [Claude Code](https://claude.com/claude-code)"


class AiAttributionTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.repo = support.git_repository(Path(self._tmp.name))

    def commit(self, message: str) -> str:
        support.git(self.repo, "commit", "-q", "--allow-empty", "-m", message)
        return support.git_output(self.repo, "rev-parse", "--short", "HEAD")

    def check(self) -> StepResult:
        return ai_attribution.run(support.context(self.repo, base="main"))

    def test_a_co_author_trailer_that_names_an_assistant_fails_naming_the_commit_and_the_line(
        self,
    ) -> None:
        short = self.commit(f"feat: x\n\n{ASSISTANT_TRAILER}")

        result = self.check()

        self.assertFalse(result.ok)
        self.assertIn(f"{short}: {ASSISTANT_TRAILER}", result.details)

    def test_a_generated_with_line_fails_naming_the_commit_and_the_line(self) -> None:
        short = self.commit(f"feat: x\n\n{GENERATED_LINE}")

        result = self.check()

        self.assertFalse(result.ok)
        self.assertIn(f"{short}: {GENERATED_LINE}", result.details)

    def test_the_assistant_is_found_by_its_address_when_the_name_is_innocent(self) -> None:
        self.commit("feat: x\n\nCo-authored-by: Helper <noreply@anthropic.com>")

        self.assertFalse(self.check().ok)

    def test_the_trailer_is_read_whatever_the_case_of_its_key(self) -> None:
        self.commit("feat: x\n\nco-authored-by: Copilot <copilot@github.com>")

        self.assertFalse(self.check().ok)

    def test_a_human_co_author_passes(self) -> None:
        self.commit("feat: x\n\nCo-authored-by: Maria Silva <maria@example.com>")

        self.assertTrue(self.check().ok)

    def test_the_dependabot_co_author_passes(self) -> None:
        self.commit(
            "build: bump\n\n"
            "Co-authored-by: dependabot[bot] <49699333+dependabot[bot]@users.noreply.github.com>"
        )

        self.assertTrue(self.check().ok)

    def test_a_message_that_only_mentions_generated_code_passes(self) -> None:
        self.commit("chore: o lock gerado com uv\n\nRegenerated with uv lock; generated with care")

        self.assertTrue(self.check().ok)

    def test_a_commit_already_in_the_base_is_not_read(self) -> None:
        support.git(self.repo, "checkout", "-q", "main")
        self.commit(f"old\n\n{ASSISTANT_TRAILER}")
        support.git(self.repo, "checkout", "-q", "change")
        support.git(self.repo, "merge", "-q", "--no-edit", "main")

        self.assertTrue(self.check().ok)

    def test_every_offending_commit_is_named_not_only_the_first(self) -> None:
        first = self.commit(f"a\n\n{ASSISTANT_TRAILER}")
        second = self.commit(f"b\n\n{GENERATED_LINE}")

        details = "\n".join(self.check().details)

        self.assertIn(first, details)
        self.assertIn(second, details)

    def test_the_step_without_a_base_fails_asking_for_it(self) -> None:
        result = ai_attribution.run(support.context(self.repo))

        self.assertFalse(result.ok)
        self.assertIn("--base", result.details[0])

    def test_a_base_that_does_not_exist_fails_naming_the_command(self) -> None:
        result = ai_attribution.run(support.context(self.repo, base="no-such-ref"))

        self.assertFalse(result.ok)
        self.assertIn("no-such-ref..HEAD", result.details[0])


class AiAttributionWorkflowTests(unittest.TestCase):
    def job(self) -> str:
        text = WORKFLOW.read_text(encoding="utf-8")
        start = text.index("  ai_attribution:\n")
        following = re.search(r"^  \w+:\n", text[start + 1 :], re.MULTILINE)
        return text[start : start + 1 + following.start()] if following else text[start:]

    def test_the_job_runs_only_on_pull_requests(self) -> None:
        self.assertIn("if: github.event_name == 'pull_request'", self.job())

    def test_the_job_fetches_the_whole_history_to_reach_the_base(self) -> None:
        self.assertIn("fetch-depth: 0", self.job())

    def test_the_job_reads_the_commits_against_the_base_of_the_pull_request(self) -> None:
        self.assertIn(
            "--step ai_attribution --base origin/${{ github.base_ref }}",
            self.job(),
        )
