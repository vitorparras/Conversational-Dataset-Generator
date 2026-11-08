"""Tests for what the mutation step shows when the initial run of Stryker.NET fails: the test that
failed and its message, read from a console and a log that the runner plays."""

from __future__ import annotations

import json
import os
import tempfile
import unittest
from collections.abc import Sequence
from dataclasses import dataclass
from pathlib import Path
from unittest import mock

from steps import mutation_initial
from steps.common import CommandOutcome

from . import support
from .mutation_fixture import MutationFixture, ToolRunner
from .mutation_samples import meta
from .test_mutation import AUTH_META, KILLED_AUTH, KILLED_LOGGING, LOGGING_META

TEST = "SyntheticStorageTests.CaTheTextIsInNeitherPlace"
CONSOLE = f"[Error] Initial testrun has more than 50% failing tests. Failing tests:\n  {TEST}\n"
LOG = (
    "2031-03-04 05:06:07 [Debug] Initial test run started\n"
    f"2031-03-04 05:06:08 [Debug] Test {TEST} failed\n"
    "  Assert.DoesNotContain() Failure: Sub-string found\n"
    "  Expected: not zqx-secret\n"
    "2031-03-04 05:06:09 [Debug] Initial test run ended\n"
)
LOG_FILE = "run/logs/log-1.txt"
RPC_FILE = "run/logs/test-servers/rpc-Synthetic.Tests-MtpRunner-0-1.log"
MESSAGE = "System.InvalidOperationException : zqx-boom raised by the fixture"
STACK = "   at Synthetic.Fixture.Observe() in Fixture.cs:line 9"


@dataclass
class InitialFailureRunner(ToolRunner):
    """Plays a Stryker that writes its log and stops at the initial run, with no report."""

    def __call__(self, command: Sequence[str], cwd: Path, seconds: float = 0.0) -> CommandOutcome:
        outcome = super().__call__(command, cwd, seconds)
        if "stryker" not in command:
            return outcome
        output = Path(command[command.index("--output") + 1])
        support.write(output, LOG_FILE, LOG)
        return CommandOutcome(False, CONSOLE)


class InitialRunTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.output = Path(self._tmp.name)

    def test_an_output_without_the_mark_explains_nothing(self) -> None:
        support.write(self.output, LOG_FILE, LOG)

        self.assertEqual(mutation_initial.explain(self.output, "[Error] build failed\n"), ())

    def test_the_cited_test_and_the_message_of_its_log_are_shown(self) -> None:
        support.write(self.output, LOG_FILE, LOG)

        lines = mutation_initial.explain(self.output, CONSOLE)

        self.assertIn(f"execução inicial do Stryker reprovou; teste(s) citado(s): {TEST}", lines[0])
        self.assertIn("  |   Assert.DoesNotContain() Failure: Sub-string found", lines)
        self.assertIn("  |   Expected: not zqx-secret", lines)

    def test_a_line_that_the_console_and_the_log_both_hold_is_shown_once(self) -> None:
        told = f"Test {TEST} failed\n"
        support.write(self.output, LOG_FILE, told + LOG)

        lines = mutation_initial.explain(self.output, CONSOLE + told)

        self.assertEqual(lines.count(f"  | {told.strip()}"), 1)

    def test_a_run_that_cites_no_test_shows_the_lines_that_tell_a_failure(self) -> None:
        support.write(self.output, LOG_FILE, LOG)
        console = "[Error] Initial testrun has more than 50% failing tests.\n"

        lines = mutation_initial.explain(self.output, console)

        self.assertIn("teste(s) citado(s): -", lines[0])
        self.assertIn("  | 2031-03-04 05:06:08 [Debug] Test " + TEST + " failed", lines)

    def test_what_is_shown_is_cut_at_the_limit(self) -> None:
        many = "".join(f"{TEST} fail {n}\n" for n in range(mutation_initial.SHOWN * 2))
        support.write(self.output, LOG_FILE, many)

        lines = mutation_initial.explain(self.output, CONSOLE)

        self.assertEqual(len(lines), mutation_initial.SHOWN + 1)

    def test_the_lines_of_the_discovery_do_not_fill_the_cut_before_the_failure(self) -> None:
        discovery = "".join(f"Discovered {TEST} {n}\n" for n in range(mutation_initial.SHOWN * 2))
        support.write(self.output, LOG_FILE, discovery + LOG)

        lines = mutation_initial.explain(self.output, CONSOLE)

        self.assertIn("  |   Expected: not zqx-secret", lines)
        self.assertFalse(any("Discovered" in line for line in lines))

    def test_a_test_named_but_never_told_as_failed_still_shows_its_lines(self) -> None:
        support.write(self.output, LOG_FILE, f"Discovered {TEST}\nnext line\n")

        lines = mutation_initial.explain(self.output, CONSOLE)

        self.assertIn(f"  | Discovered {TEST}", lines)

    def test_the_tests_are_named_once_each_in_the_order_they_come(self) -> None:
        text = "A.BTests.One and C.DTests.Two then BTests.One again"

        self.assertEqual(mutation_initial.cited(text), ("BTests.One", "DTests.Two"))


class InitialRunStepTests(MutationFixture):
    def test_the_step_shows_the_failed_test_and_its_message(self) -> None:
        runner = InitialFailureRunner(
            metas={AUTH_META: meta({KILLED_AUTH: 1}), LOGGING_META: meta({KILLED_LOGGING: 1})}
        )

        result = self.run_step(runner, "Core.Platform.Logging")

        module = "Registro do núcleo (`Core.Platform.Logging`)"
        self.assertFalse(result.ok)
        self.assertIn(f"{module}: o Stryker não escreveu relatório", result.details)
        message = "Assert.DoesNotContain() Failure: Sub-string found"
        self.assertIn(f"{module}:   |   {message}", result.details)
        self.assertTrue(any(TEST in line for line in result.details))

    def test_stryker_is_asked_to_write_its_log_by_the_command_line_and_not_the_file(self) -> None:
        runner = InitialFailureRunner()

        self.run_step(runner, "Core.Platform.Logging")

        config = self.root / ".verify-code/mutation/core/Core.Platform.Logging/stryker-config.json"
        self.assertEqual(len(runner.called("--log-to-file")), 1)
        self.assertNotIn("log-to-file", config.read_text(encoding="utf-8"))


def node(name: str, state: str, message: str = "", stack: str = "") -> dict[str, object]:
    """A test node as the host sends it in an update of the talk."""
    found: dict[str, object] = {
        "uid": f"uid-{name}",
        "display-name": name,
        "execution-state": state,
    }
    if message:
        found["error.message"] = message
        found["error.stacktrace"] = stack
    return found


def talk(*nodes: dict[str, object]) -> str:
    """The lines the talk log holds: noise, an update with the nodes, and updates without."""
    update = {
        "method": "testing/testUpdates/tests",
        "params": {"changes": [{"node": n} for n in nodes]},
    }
    return (
        "JsonRpc Information: 3 : Listening started.\n"
        f"JsonRpc Verbose: 7 : Received: {json.dumps(update)}\n"
        'JsonRpc Verbose: 7 : Received: {"jsonrpc":"2.0","id":2,"result":{}}\n'
        "JsonRpc Verbose: 7 : Received: not json at all\n"
    )


class InitialRunTalkTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.output = Path(self._tmp.name)

    def test_the_message_and_the_stack_of_the_failed_test_are_shown(self) -> None:
        support.write(self.output, RPC_FILE, talk(node(TEST, "error", MESSAGE, STACK)))

        lines = mutation_initial.explain(self.output, CONSOLE)

        self.assertIn(f"execução inicial do Stryker reprovou; teste(s) citado(s): {TEST}", lines[0])
        self.assertIn(f"  | {TEST} (estado error)", lines)
        self.assertIn(f"  |   {MESSAGE}", lines)
        self.assertIn(f"  |   {STACK.strip()}", lines)

    def test_a_test_that_passed_or_was_skipped_is_not_a_failure(self) -> None:
        ok = (node("A.Tests.One", "passed"), node("A.Tests.Two", "skipped"))
        support.write(self.output, RPC_FILE, talk(*ok, node(TEST, "failed", MESSAGE)))

        failures = mutation_initial.failed_tests(self.output)

        self.assertEqual([failure.name for failure in failures], [TEST])

    def test_the_talk_wins_over_the_other_lines_of_the_logs(self) -> None:
        support.write(self.output, LOG_FILE, LOG)
        support.write(self.output, RPC_FILE, talk(node(TEST, "error", MESSAGE)))

        lines = mutation_initial.explain(self.output, CONSOLE)

        self.assertFalse(any("Initial test run started" in line for line in lines))

    def test_a_long_message_is_cut(self) -> None:
        message = "\n".join(f"line {n}" for n in range(mutation_initial.MESSAGE_LINES * 3))
        support.write(self.output, RPC_FILE, talk(node(TEST, "error", message)))

        lines = mutation_initial.explain(self.output, CONSOLE)

        self.assertIn("  |   line 0", lines)
        self.assertNotIn(f"  |   line {mutation_initial.MESSAGE_LINES}", lines)

    def test_the_summary_names_the_test_the_state_and_the_message(self) -> None:
        support.write(self.output, RPC_FILE, talk(node(TEST, "error", MESSAGE, STACK)))

        text = mutation_initial.summary(self.output, CONSOLE, "Synthetic.Module")

        self.assertIn("Synthetic.Module", text)
        self.assertIn(f"**`{TEST}`**, estado `error`", text)
        self.assertIn(MESSAGE, text)
        self.assertIn(STACK.strip(), text)

    def test_the_summary_is_empty_without_the_mark_or_without_a_failed_node(self) -> None:
        support.write(self.output, RPC_FILE, talk(node(TEST, "error", MESSAGE)))
        self.assertEqual(mutation_initial.summary(self.output, "[Error] build failed\n", "M"), "")
        self.assertEqual(mutation_initial.summary(self.output / "none", CONSOLE, "M"), "")


class InitialRunSummaryStepTests(MutationFixture):
    def test_the_step_writes_the_failed_test_into_the_job_summary(self) -> None:
        class Runner(InitialFailureRunner):
            def __call__(
                self, command: Sequence[str], cwd: Path, seconds: float = 0.0
            ) -> CommandOutcome:
                outcome = super().__call__(command, cwd, seconds)
                if "stryker" in command:
                    output = Path(command[command.index("--output") + 1])
                    support.write(output, RPC_FILE, talk(node(TEST, "error", MESSAGE, STACK)))
                return outcome

        summary = self.root / "summary.md"
        with mock.patch.dict(os.environ, {"GITHUB_STEP_SUMMARY": str(summary)}):
            self.run_step(Runner(), "Core.Platform.Logging")

        text = summary.read_text(encoding="utf-8")
        self.assertIn("Core.Platform.Logging", text)
        self.assertIn(TEST, text)
        self.assertIn(MESSAGE, text)
