"""Tests for the offline_start step, with a fake runner in place of docker, git and dotnet."""

from __future__ import annotations

import tempfile
import unittest
from collections.abc import Callable, Mapping, Sequence
from dataclasses import dataclass, field
from pathlib import Path

from steps import offline_containers as containers
from steps import offline_start, package
from steps.common import OUTPUT_DIRECTORY, CommandOutcome, Context, StepResult

from . import support

WORKFLOW = support.VERIFY_CODE.parents[1] / ".github" / "workflows" / "verify-code.yml"
LISTENING = (
    "2026-01-01T00:00:00.000Z INFO  Microsoft.Hosting.Lifetime[14] "
    "Now listening on: http://127.0.0.1:5000 address=http://127.0.0.1:5000\n"
)
VERSIONS = (
    "2026-01-01T00:00:00.000Z INFO  Core.Host[200] Versões em execução SchemaVersion=2 "
    "ModelVersion=<absent> LanguageServiceVersion=0.1.0 ContractVersion=1 CoreVersion=0.1.0\n"
)
FAILURE = CommandOutcome(False, "ZQX-MARKER-3318 failed", 1)
COMMIT = "a" * 40
NOT_READY = "a partida não chegou à linha de pronto (`Now listening on`)"


def codes(migrate: int = 0, serve: int = 0, ready: str = "yes") -> str:
    return f"migrate={migrate}\nserve={serve}\nready={ready}\n"


def evidence(**changes: str) -> dict[str, str]:
    """The files a host that migrated and served leaves, with the ``changes`` over them."""
    files = {
        "interfaces.txt": "lo\n",
        "migrate.log": "Esquema migrado até a versão 2\n",
        "serve.log": VERSIONS + LISTENING,
        "result.txt": codes(),
    }
    return files | changes


@dataclass
class DockerRunner:
    """Answers docker, git and dotnet as they would; the container's /trace holds ``files``."""

    files: Mapping[str, str]
    failing: str = ""
    breaks: Callable[[Sequence[str]], bool] = lambda command: False
    commands: list[list[str]] = field(default_factory=list[list[str]])

    def __call__(self, command: Sequence[str], cwd: Path) -> CommandOutcome:
        self.commands.append(list(command))
        if self.failing in command or self.breaks(command):
            return FAILURE
        if command[:2] == ["git", "rev-parse"]:
            return CommandOutcome(True, COMMIT + "\n")
        if "publish" in command:
            output = Path(command[command.index("--output") + 1])
            (output / package.executable(containers.SYSTEM)).write_bytes(b"host")
            (output / package.SETTINGS_FILE).write_bytes(b"{}")
        if command[:2] == ["docker", "cp"] and command[-2].endswith(":/trace/."):
            for name, content in self.files.items():
                (Path(command[-1]) / name).write_text(content, encoding="utf-8")
        return support.SUCCESS

    def docker(self, *verbs: str) -> list[list[str]]:
        """The docker commands recorded so far that begin with ``verbs``."""
        size = len(verbs)
        return [c for c in self.commands if c[0] == "docker" and tuple(c[1 : 1 + size]) == verbs]

    def position(self, *verbs: str) -> int:
        """Where in the order of commands the first docker command with ``verbs`` was given."""
        return self.commands.index(self.docker(*verbs)[0])


class OfflineStartTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)
        support.write(self.root, package.PROJECT, "<Project />\n")

    def run_step(self, runner: DockerRunner) -> StepResult:
        return offline_start.run(Context(self.root, self.root / OUTPUT_DIRECTORY, runner))

    def fails_with(self, **changes: str) -> tuple[str, ...]:
        """The details of the step when the container leaves the evidence with ``changes``."""
        result = self.run_step(DockerRunner(evidence(**changes)))
        self.assertFalse(result.ok)
        return result.details

    def test_a_start_that_serves_with_everything_present_and_stays_on_the_loopback_passes(
        self,
    ) -> None:
        result = self.run_step(DockerRunner(evidence()))

        self.assertTrue(result.ok, result.details)
        self.assertIn("banco, pasta e serviço de pé", result.details[0])

    def test_the_server_has_no_network_and_the_host_joins_its_namespace(self) -> None:
        runner = DockerRunner(evidence())

        self.run_step(runner)

        server = runner.docker("run", "--detach")[0]
        host = runner.docker("create", "--name", containers.HOST)[0]
        self.assertEqual(server[server.index("--network") + 1], "none")
        self.assertEqual(host[host.index("--network") + 1], f"container:{containers.DATABASE}")

    def test_the_folder_reaches_the_host_read_only_from_the_volume_the_build_filled(self) -> None:
        runner = DockerRunner(evidence())

        self.run_step(runner)

        build = runner.docker("create", "--name", containers.BUILD)[0]
        mount = runner.docker("create", "--name", containers.HOST)[0][-2]
        self.assertIn(f"{containers.VOLUME}:/out", build)
        self.assertIn(f"type=volume,src={containers.VOLUME},dst=/work/language-service", mount)
        self.assertTrue(mount.endswith(",readonly"))

    def test_the_build_container_assembles_the_folder_for_the_system_with_the_model(self) -> None:
        runner = DockerRunner(evidence())

        self.run_step(runner)

        script = runner.docker("create", "--name", containers.BUILD)[0][-1]
        self.assertIn(f"--system {containers.SYSTEM} --model {containers.MODEL}", script)

    def test_the_folder_is_assembled_and_the_server_is_up_before_the_host_starts(self) -> None:
        runner = DockerRunner(evidence())

        self.run_step(runner)

        starting = runner.position("start", "--attach", containers.HOST)
        self.assertLess(runner.position("start", "--attach", containers.BUILD), starting)
        self.assertLess(runner.position("exec", containers.DATABASE), starting)

    def test_the_passwords_of_the_server_are_not_in_the_report(self) -> None:
        runner = DockerRunner(evidence(**{"serve.log": "no line\n"}))

        result = self.run_step(runner)

        application = runner.docker("run", "--detach")[0][-2].split("=", 1)[1]
        self.assertNotIn(application, "\n".join(result.details))

    def test_every_run_gets_passwords_of_its_own_and_distinct_ones(self) -> None:
        first, second = containers.Credentials(), containers.Credentials()

        self.assertEqual(len({*vars(first).values(), *vars(second).values()}), 6)

    def test_every_container_and_the_volume_are_removed_when_the_step_ends(self) -> None:
        runner = DockerRunner(evidence())

        self.run_step(runner)

        self.assertEqual(runner.commands[-2], ["docker", "rm", "--force", *containers.CONTAINERS])
        self.assertEqual(
            runner.commands[-1], ["docker", "volume", "rm", "--force", containers.VOLUME]
        )

    def test_the_containers_are_removed_when_a_stage_fails(self) -> None:
        runner = DockerRunner(evidence(), failing="start")

        result = self.run_step(runner)

        self.assertFalse(result.ok)
        self.assertEqual(runner.commands[-1][:3], ["docker", "volume", "rm"])
        self.assertEqual(runner.docker("run", "--detach"), [])

    def test_a_start_that_never_printed_the_ready_line_fails(self) -> None:
        self.assertIn(NOT_READY, self.fails_with(**{"serve.log": VERSIONS}))

    def test_a_host_that_reports_not_ready_fails_even_with_the_line_in_its_output(self) -> None:
        self.assertIn(NOT_READY, self.fails_with(**{"result.txt": codes(ready="no")}))

    def test_a_migration_that_does_not_end_with_zero_fails(self) -> None:
        details = self.fails_with(**{"result.txt": codes(migrate=71)})

        self.assertIn("migrate saiu com código 71, esperado 0", details)

    def test_a_host_that_does_not_stop_with_zero_fails(self) -> None:
        details = self.fails_with(**{"result.txt": codes(serve=137)})

        self.assertIn("serve saiu com código 137, esperado 0", details)

    def test_a_versions_line_without_the_service_fails_naming_it(self) -> None:
        versions = VERSIONS.replace(
            "LanguageServiceVersion=0.1.0", "LanguageServiceVersion=<absent>"
        )

        details = self.fails_with(**{"serve.log": versions + LISTENING})

        self.assertEqual(details[0], "LanguageServiceVersion ausente na linha de versões")

    def test_a_versions_line_without_the_schema_fails_naming_it(self) -> None:
        versions = VERSIONS.replace("SchemaVersion=2", "SchemaVersion=<absent>")

        details = self.fails_with(**{"serve.log": versions + LISTENING})

        self.assertEqual(details[0], "SchemaVersion ausente na linha de versões")

    def test_a_namespace_with_an_interface_besides_the_loopback_fails(self) -> None:
        details = self.fails_with(**{"interfaces.txt": "eth0\nlo\n"})

        self.assertIn("o namespace não ficou só com o loopback", details[0])

    def test_a_host_container_that_wrote_nothing_fails_with_every_reason(self) -> None:
        result = self.run_step(DockerRunner({}))

        self.assertFalse(result.ok)
        self.assertEqual(len(result.details), 7, result.details)

    def test_every_problem_is_reported_not_only_the_first(self) -> None:
        details = self.fails_with(**{"result.txt": codes(1, 2, "no"), "serve.log": ""})

        self.assertGreaterEqual(len(details), 6)

    def test_a_host_container_that_cannot_be_created_fails_with_the_output_of_docker(self) -> None:
        runner = DockerRunner(evidence(), failing=f"container:{containers.DATABASE}")

        result = self.run_step(runner)

        self.assertEqual(result.details[0], "o contêiner do núcleo não rodou até o fim")
        self.assertIn("  | ZQX-MARKER-3318 failed", result.details)

    def test_a_host_container_that_cannot_be_started_fails_with_the_output_of_docker(self) -> None:
        start = ["docker", "start", "--attach", containers.HOST]
        runner = DockerRunner({}, breaks=lambda command: list(command) == start)

        result = self.run_step(runner)

        self.assertEqual(result.details[0], "o contêiner do núcleo não rodou até o fim")
        self.assertNotIn("migrate saiu com código nenhum, esperado 0", result.details)

    def test_a_migration_that_runs_past_its_deadline_is_named(self) -> None:
        details = self.fails_with(**{"result.txt": codes(migrate=124)})

        self.assertIn("migrate passou do prazo e foi interrompido", details)

    def test_a_failed_image_build_names_the_image_and_creates_no_container(self) -> None:
        runner = DockerRunner(evidence(), failing="build")

        result = self.run_step(runner)

        self.assertIn("a imagem cdg-offline-build:verify não foi construída", result.details)
        self.assertEqual(runner.docker("create"), [])

    def test_a_folder_that_cannot_be_assembled_fails_before_the_server_starts(self) -> None:
        runner = DockerRunner(evidence(), failing=containers.BUILD)

        result = self.run_step(runner)

        self.assertIn("a pasta do serviço não foi montada", result.details)
        self.assertEqual(runner.docker("run", "--detach"), [])

    def test_a_server_that_does_not_answer_fails_before_the_host_is_created(self) -> None:
        runner = DockerRunner(evidence(), failing="exec")

        result = self.run_step(runner)

        self.assertIn("o servidor do banco não ficou de pé", result.details)
        self.assertEqual(runner.docker("create", "--name", containers.HOST), [])

    def test_a_failed_publish_fails_under_the_name_of_this_step(self) -> None:
        runner = DockerRunner(evidence(), failing="publish")

        result = self.run_step(runner)

        self.assertEqual((result.ok, result.name), (False, offline_start.NAME))
        self.assertEqual(runner.docker("volume", "create"), [])

    def test_a_missing_project_fails_without_running_anything(self) -> None:
        (self.root / package.PROJECT).unlink()
        runner = DockerRunner(evidence())

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
