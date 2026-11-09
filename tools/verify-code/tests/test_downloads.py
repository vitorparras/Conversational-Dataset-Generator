"""Tests for the retry of what the CI downloads (#733, CA-4 and CA-5)."""

from __future__ import annotations

import os
import sys
import tempfile
import unittest
from collections.abc import Sequence
from pathlib import Path
from unittest import mock

import retry

from steps import coverage_by_area, downloads
from steps.common import CommandOutcome, RunCommand

URL = "https://files.pythonhosted.org/packages/ab/cd/spacy-3.8.whl"
GATEWAY = CommandOutcome(False, f"ERROR: HTTP error 504 while getting {URL}\n", 1)
RESET = CommandOutcome(False, "curl: (56) Recv failure: Connection reset by peer\n", 56)
DNS = CommandOutcome(False, "Could not resolve host: github.com\n", 6)
BROKEN = CommandOutcome(False, "ERROR: No matching distribution found for zqx-package\n", 1)
FAILED_TEST = CommandOutcome(False, "Failed ZqxTests.Waits: expected 504 rows\n", 1)
GOOD = CommandOutcome(True, "ok\n", 0)
PIP = ["pip", "install", "-r", "requirements.txt"]
CURL = ["curl", "-sSfL", "https://github.com/gitleaks/gitleaks/releases/download/v8/g.tar.gz"]


class Attempts:
    """The outcomes of the tries in order, the waits asked and the summaries written."""

    def __init__(self, *outcomes: CommandOutcome) -> None:
        self.outcomes = list(outcomes)
        self.waits: list[float] = []
        self.summaries: list[str] = []

    def __call__(self) -> CommandOutcome:
        return self.outcomes.pop(0)

    def repeat(self, command: Sequence[str]) -> CommandOutcome:
        return downloads.repeat(
            self, command, sleep=self.waits.append, publish=self.summaries.append
        )


class WhichCommandsDownloadTests(unittest.TestCase):
    def test_the_tools_and_verbs_that_bring_something_from_the_network_are_downloads(self) -> None:
        commands = (
            PIP,
            ["uv", "sync", "--frozen"],
            ["/usr/bin/curl", "-sSfL", "https://x.invalid/y"],
            ["dotnet", "restore", "core/Core.slnx", "--locked-mode"],
            ["dotnet", "tool", "restore"],
            ["dotnet", "build", "core/Core.slnx"],
            ["dotnet", "publish", "core/src/Core.Host/Core.Host.csproj"],
            ["docker", "build", "--tag", "x", "."],
            ["docker", "pull", "postgres:17"],
            ["pwsh", "playwright.ps1", "install", "chromium"],
            ["python", "-m", "pip", "install", "x"],
            ["gh", "run", "download", "1"],
            ["trivy", "fs", "."],
        )
        for command in commands:
            with self.subTest(command=command):
                self.assertTrue(downloads.is_download(command))

    def test_a_test_a_run_a_build_without_restore_and_git_are_not(self) -> None:
        commands: tuple[list[str], ...] = (
            ["dotnet", "test", "core/Core.slnx"],
            ["dotnet", "run", "--project", "x"],
            ["dotnet", "build", "core/Core.slnx", "--no-restore"],
            ["dotnet", "format", "core/Core.slnx"],
            ["docker", "run", "--rm", "x"],
            ["git", "status"],
            ["gitleaks", "git"],
            [],
        )
        for command in commands:
            with self.subTest(command=command):
                self.assertFalse(downloads.is_download(command))


class WhatTellsTheNetworkTests(unittest.TestCase):
    def test_a_gateway_a_reset_and_a_name_that_does_not_resolve_are_the_network(self) -> None:
        for outcome in (GATEWAY, RESET, DNS):
            with self.subTest(text=outcome.text):
                self.assertTrue(downloads.failure_lines(outcome.text))

    def test_a_failure_of_the_command_itself_is_not_the_network(self) -> None:
        for outcome in (BROKEN, CommandOutcome(False, "E: Unable to locate package x\n")):
            with self.subTest(text=outcome.text):
                self.assertEqual(downloads.failure_lines(outcome.text), ())

    def test_a_number_alone_is_not_a_status(self) -> None:
        self.assertEqual(downloads.failure_lines("rows: 504\n"), ())

    def test_the_url_of_the_output_and_the_http_code_are_the_evidence(self) -> None:
        found = downloads.evidence(PIP, GATEWAY.text)

        self.assertEqual((found.urls, found.codes), ((URL,), ("HTTP 504",)))

    def test_the_url_of_the_command_and_the_code_of_curl_are_the_evidence(self) -> None:
        found = downloads.evidence(CURL, RESET.text)

        self.assertEqual((found.urls, found.codes), ((CURL[-1],), ("curl (56)",)))

    def test_a_failure_with_no_number_gives_what_the_line_said(self) -> None:
        found = downloads.evidence(CURL, DNS.text)

        self.assertEqual(found.codes, ("could not resolve",))


class RepeatTests(unittest.TestCase):
    def test_a_download_that_comes_at_the_first_try_is_run_once_and_says_nothing(self) -> None:
        attempts = Attempts(GOOD)

        outcome = attempts.repeat(PIP)

        self.assertEqual((outcome, attempts.waits, attempts.summaries), (GOOD, [], []))

    def test_a_gateway_error_is_tried_again_after_a_wait_and_the_summary_says_it_was_repaired(
        self,
    ) -> None:
        attempts = Attempts(GATEWAY, GOOD)

        outcome = attempts.repeat(PIP)

        self.assertTrue(outcome.ok)
        self.assertEqual(attempts.waits, [10.0])
        self.assertEqual(len(attempts.summaries), 1)
        self.assertIn("Download repetido e concluído", attempts.summaries[0])
        self.assertIn(URL, attempts.summaries[0])
        self.assertIn("HTTP 504", attempts.summaries[0])
        self.assertIn("tentativas: 2 de 4", attempts.summaries[0])

    def test_the_wait_grows_and_the_tries_stop_at_four(self) -> None:
        attempts = Attempts(GATEWAY, GATEWAY, GATEWAY, GATEWAY)

        outcome = attempts.repeat(PIP)

        self.assertFalse(outcome.ok)
        self.assertEqual(attempts.waits, [10.0, 30.0, 90.0])
        self.assertEqual(attempts.outcomes, [])

    def test_the_summary_of_the_one_that_gave_up_names_the_url_and_the_code(self) -> None:
        attempts = Attempts(GATEWAY, GATEWAY, GATEWAY, GATEWAY)

        _ = attempts.repeat(PIP)

        summary = attempts.summaries[-1]
        self.assertEqual(len(attempts.summaries), 1)
        self.assertIn("### Download que caiu", summary)
        self.assertIn(f"- URL: {URL}", summary)
        self.assertIn("- código: HTTP 504", summary)
        self.assertIn("tentativas: 4 de 4", summary)

    def test_a_failure_that_is_not_the_network_is_returned_at_once_with_no_summary(self) -> None:
        for failure in (BROKEN, FAILED_TEST):
            with self.subTest(text=failure.text):
                attempts = Attempts(failure)

                outcome = attempts.repeat(PIP)

                self.assertEqual((outcome, attempts.waits, attempts.summaries), (failure, [], []))

    def test_a_failure_that_changes_kind_stops_the_tries_and_keeps_the_evidence_of_the_network(
        self,
    ) -> None:
        attempts = Attempts(GATEWAY, BROKEN)

        outcome = attempts.repeat(PIP)

        self.assertEqual((outcome, attempts.waits), (BROKEN, [10.0]))
        self.assertIn("Download que caiu", attempts.summaries[0])


class RetryingRunnerTests(unittest.TestCase):
    def runner(self, *outcomes: CommandOutcome) -> tuple[list[list[str]], list[float], RunCommand]:
        calls: list[list[str]] = []
        waits: list[float] = []
        queue = list(outcomes)

        def run(command: Sequence[str], cwd: Path) -> CommandOutcome:
            _ = cwd
            calls.append(list(command))
            return queue.pop(0)

        return calls, waits, downloads.retrying(run, sleep=waits.append, publish=lambda _: None)

    def test_a_download_is_run_again_through_the_runner(self) -> None:
        calls, waits, run = self.runner(GATEWAY, GOOD)

        outcome = run(PIP, Path())

        self.assertEqual((outcome.ok, len(calls), waits), (True, 2, [10.0]))

    def test_a_test_run_is_never_run_again_even_when_its_output_tells_a_504(self) -> None:
        calls, waits, run = self.runner(GATEWAY)

        outcome = run(["dotnet", "test", "core/Core.slnx"], Path())

        self.assertEqual((outcome.ok, len(calls), waits), (False, 1, []))


class CommandLineTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.summary = Path(self._tmp.name) / "summary.md"
        environment = mock.patch.dict(os.environ, {coverage_by_area.SUMMARY: str(self.summary)})
        environment.start()
        self.addCleanup(environment.stop)
        sleeping = mock.patch.object(retry.time, "sleep")
        self.sleeping = sleeping.start()
        self.addCleanup(sleeping.stop)

    def python(self, code: str) -> list[str]:
        return ["--", sys.executable, "-c", code]

    def test_a_command_that_comes_returns_zero(self) -> None:
        self.assertEqual(retry.main(self.python("print('ok')")), 0)

    def test_a_download_that_never_comes_returns_its_code_after_four_tries_and_writes_the_url(
        self,
    ) -> None:
        code = f"import sys; print('HTTP error 504 getting {URL}'); sys.exit(3)"

        result = retry.main(self.python(code))

        self.assertEqual(result, 3)
        self.assertEqual(self.sleeping.call_count, 3)
        text = self.summary.read_text(encoding="utf-8")
        self.assertIn(URL, text)
        self.assertIn("HTTP 504", text)

    def test_a_failure_of_the_command_itself_is_not_repeated(self) -> None:
        result = retry.main(self.python("import sys; print('bad requirement'); sys.exit(2)"))

        self.assertEqual((result, self.sleeping.call_count), (2, 0))

    def test_a_command_that_does_not_exist_returns_127_and_a_call_without_command_is_refused(
        self,
    ) -> None:
        self.assertEqual(retry.main(["--", "zqx-no-such-tool-733"]), retry.NOT_FOUND)
        self.assertEqual(retry.main(["--"]), retry.USAGE)
        self.assertEqual(retry.main([]), retry.USAGE)


if __name__ == "__main__":
    unittest.main()
