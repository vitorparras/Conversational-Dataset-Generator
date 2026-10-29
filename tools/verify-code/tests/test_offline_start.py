"""Tests for the offline_start step, with a fake runner in place of docker and dotnet."""

from __future__ import annotations

import tempfile
import unittest
from collections.abc import Mapping, Sequence
from dataclasses import dataclass, field
from pathlib import Path

from steps import offline_start, package
from steps.common import OUTPUT_DIRECTORY, CommandOutcome, Context, StepResult

from . import support

WORKFLOW = support.VERIFY_CODE.parents[1] / ".github" / "workflows" / "verify-code.yml"
LISTENING = (
    "2026-01-01T00:00:00.000Z INFO  Microsoft.Hosting.Lifetime[14] "
    "Now listening on: http://127.0.0.1:5000 address=http://127.0.0.1:5000\n"
)
BIND = (
    'bind(173, {sa_family=AF_INET, sin_port=htons(5000), sin_addr=inet_addr("127.0.0.1")}, 16) = 0'
)
EXTERNAL = (
    'connect(9, {sa_family=AF_INET, sin_port=htons(443), sin_addr=inet_addr("192.0.2.7")}, 16) = -1'
)
QUERY = (
    'sendto(9, "\\1", 1, 0, {sa_family=AF_INET, sin_port=htons(53), '
    'sin_addr=inet_addr("192.0.2.53")}, 16) = 1'
)
FAILURE = CommandOutcome(False, "ZQX-MARKER-3318 failed", 1)
STOPPED = CommandOutcome(True, "", 0)


def traced(*calls: str) -> str:
    """The calls as the trace of the host, one per line."""
    return "".join(f"13<Core.Host> {call}\n" for call in calls)


@dataclass
class ContainerRunner:
    """Answers docker as a container would, and dotnet as a publish; records every command."""

    files: Mapping[str, str]
    started: CommandOutcome = STOPPED
    failing: str = ""
    commands: list[list[str]] = field(default_factory=list[list[str]])

    def __call__(self, command: Sequence[str], cwd: Path) -> CommandOutcome:
        self.commands.append(list(command))
        if self.failing in command:
            return FAILURE
        if "publish" in command:
            output = Path(command[command.index("--output") + 1])
            (output / package.executable(offline_start.TARGET)).write_bytes(b"host")
            (output / package.SETTINGS_FILE).write_bytes(b"{}")
        if command[:2] == ["docker", "cp"] and command[-2].endswith(":/trace/."):
            self.copy_trace(Path(command[-1]))
        return self.started if command[:2] == ["docker", "start"] else support.SUCCESS

    def copy_trace(self, destination: Path) -> None:
        for name, content in self.files.items():
            (destination / name).write_text(content, encoding="utf-8")

    def docker(self, verb: str) -> list[list[str]]:
        """The docker commands recorded so far that use ``verb``."""
        return [c for c in self.commands if c[:2] == ["docker", verb]]


def start(*calls: str, host: str = LISTENING, interfaces: str = "lo\n") -> dict[str, str]:
    """The files the container leaves: the interfaces it saw, the host's output and the trace."""
    return {"interfaces.txt": interfaces, "host.log": host, "strace.txt": traced(*calls)}


class OfflineStartTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)
        support.write(self.root, package.PROJECT, "<Project />\n")

    def run_step(self, runner: ContainerRunner) -> StepResult:
        return offline_start.run(Context(self.root, self.root / OUTPUT_DIRECTORY, runner))

    def test_a_start_that_serves_and_stops_with_zero_and_stays_on_the_loopback_passes(self) -> None:
        runner = ContainerRunner(start(BIND))

        result = self.run_step(runner)

        self.assertTrue(result.ok, result.details)
        self.assertIn("serviu e saiu com 0", result.details[0])
        self.assertIn("bind 127.0.0.1:5000", result.details[1])

    def test_a_passing_start_lists_what_the_tracer_saw(self) -> None:
        result = self.run_step(ContainerRunner(start(BIND, BIND.replace("bind", "connect"))))

        self.assertIn(
            "bind 127.0.0.1:5000, connect 127.0.0.1:5000", result.details[1], result.details
        )

    def test_the_container_is_created_without_a_network(self) -> None:
        runner = ContainerRunner(start(BIND))

        self.run_step(runner)

        create = runner.docker("create")[0]
        self.assertEqual(create[create.index("--network") + 1], "none")

    def test_the_published_folder_is_copied_into_the_container_before_it_starts(self) -> None:
        runner = ContainerRunner(start(BIND))

        self.run_step(runner)

        verbs = [c[1] for c in runner.commands if c[0] == "docker"]
        self.assertLess(verbs.index("cp"), verbs.index("start"))

    def test_the_container_is_removed_when_the_step_ends(self) -> None:
        runner = ContainerRunner(start(BIND))

        self.run_step(runner)

        self.assertEqual(runner.commands[-1][:3], ["docker", "rm", "--force"])

    def test_the_container_is_removed_when_it_cannot_be_created(self) -> None:
        runner = ContainerRunner(start(BIND), failing="create")

        result = self.run_step(runner)

        self.assertFalse(result.ok)
        self.assertEqual(runner.commands[-1][:3], ["docker", "rm", "--force"])
        self.assertEqual(runner.docker("start"), [])

    def test_a_connection_outside_the_loopback_fails_naming_call_destination_and_process(
        self,
    ) -> None:
        result = self.run_step(ContainerRunner(start(BIND, EXTERNAL)))

        self.assertFalse(result.ok)
        self.assertIn(
            "conexão fora do loopback: connect para 192.0.2.7:443 (processo Core.Host, pid 13)",
            result.details,
        )

    def test_a_dns_query_fails_naming_the_call(self) -> None:
        result = self.run_step(ContainerRunner(start(BIND, QUERY)))

        self.assertFalse(result.ok)
        self.assertIn(
            "consulta de DNS: sendto para 192.0.2.53:53 (processo Core.Host, pid 13)",
            result.details,
        )

    def test_a_start_that_never_printed_the_ready_line_fails(self) -> None:
        result = self.run_step(ContainerRunner(start(BIND, host="starting\n")))

        self.assertFalse(result.ok)
        self.assertIn("a partida não chegou à linha de pronto (`Now listening on`)", result.details)
        self.assertIn("  | starting", result.details)

    def test_an_exit_code_other_than_zero_fails_even_after_the_ready_line(self) -> None:
        runner = ContainerRunner(start(BIND), started=CommandOutcome(False, "", 70))

        result = self.run_step(runner)

        self.assertFalse(result.ok)
        self.assertIn("o núcleo saiu com código 70, esperado 0", result.details)

    def test_a_container_with_an_interface_besides_the_loopback_fails(self) -> None:
        result = self.run_step(ContainerRunner(start(BIND, interfaces="eth0\nlo\n")))

        self.assertFalse(result.ok)
        self.assertIn("interfaces lidas: ['eth0', 'lo']", result.details[0])

    def test_an_empty_trace_fails_because_without_a_trace_nothing_is_proved(self) -> None:
        result = self.run_step(ContainerRunner(start()))

        self.assertFalse(result.ok)
        self.assertIn("o rastreador não registrou nada: sem rastro não há prova", result.details)

    def test_a_container_that_wrote_nothing_fails_with_every_reason(self) -> None:
        runner = ContainerRunner({}, started=CommandOutcome(False, "ZQX-MARKER-2201", 125))

        result = self.run_step(runner)

        self.assertFalse(result.ok)
        self.assertEqual(len(result.details), 5, result.details)
        self.assertIn("  | ZQX-MARKER-2201", result.details)

    def test_every_problem_is_reported_not_only_the_first(self) -> None:
        result = self.run_step(ContainerRunner(start(BIND, EXTERNAL, QUERY, host="")))

        self.assertGreaterEqual(len(result.details), 4)

    def test_a_failed_image_build_fails_with_the_output_of_docker(self) -> None:
        runner = ContainerRunner(start(BIND), failing="build")

        result = self.run_step(runner)

        self.assertFalse(result.ok)
        self.assertIn("  | ZQX-MARKER-3318 failed", result.details)
        self.assertEqual(runner.docker("create"), [])

    def test_a_failed_publish_fails_under_the_name_of_this_step(self) -> None:
        runner = ContainerRunner(start(BIND), failing="publish")

        result = self.run_step(runner)

        self.assertEqual((result.ok, result.name), (False, offline_start.NAME))
        self.assertEqual(runner.docker("create"), [])

    def test_a_missing_project_fails_without_running_anything(self) -> None:
        (self.root / package.PROJECT).unlink()
        runner = ContainerRunner(start(BIND))

        result = self.run_step(runner)

        self.assertFalse(result.ok)
        self.assertEqual(runner.commands, [])


class OfflineStartWorkflowTests(unittest.TestCase):
    def setUp(self) -> None:
        text = WORKFLOW.read_text(encoding="utf-8")
        self.job = support.workflow_job(text, offline_start.NAME)

    def test_the_job_runs_the_step_on_linux(self) -> None:
        self.assertIn("runs-on: ubuntu-latest", self.job)
        self.assertIn(f"verify.py --step {offline_start.NAME}", self.job)

    def test_the_job_also_runs_in_the_scheduled_execution(self) -> None:
        self.assertNotIn("if:", self.job)

    def test_the_job_has_the_sdk_the_publish_needs(self) -> None:
        self.assertIn("actions/setup-dotnet@", self.job)


if __name__ == "__main__":
    unittest.main()
