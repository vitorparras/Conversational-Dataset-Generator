"""Tests for the database container a core module's tests may start: the mutation step asks the test
project to keep one container across the restarts of Stryker's test server and always removes it."""

from __future__ import annotations

import os
from collections.abc import Sequence
from pathlib import Path
from unittest import mock

from steps import mutation, mutation_core
from steps.common import OUTPUT_DIRECTORY, CommandOutcome, Context

from . import mutation_fixture as fixture
from . import support
from .test_mutation import healthy

PROJECT = "core/tests/Core.Platform.Tests/Core.Platform.Tests.csproj"
REFERENCE = r'<ProjectReference Include="..\..\src\Core.Platform\Core.Platform.csproj" />'
DATABASE_TESTS = f'<Project>{REFERENCE}<PackageReference Include="Testcontainers" /></Project>'
MODULE = "Core.Platform.Logging"
VARIABLE = mutation_core.REUSE_VARIABLE


class Watched(fixture.ToolRunner):
    """The runner of the fixture, which also notes the reuse variable each command ran with."""

    def __init__(self, stryker: str, raising: BaseException | None = None) -> None:
        super().__init__(stryker=stryker)
        self.seen: list[str | None] = []
        self.raising = raising

    def __call__(self, command: Sequence[str], cwd: Path, seconds: float = 0.0) -> CommandOutcome:
        self.seen.append(os.environ.get(VARIABLE))
        if self.raising is not None and "stryker" in command:
            raise self.raising
        return super().__call__(command, cwd, seconds)


class MutationDatabaseTests(fixture.MutationFixture):
    def setUp(self) -> None:
        super().setUp()
        support.write(self.root, PROJECT, DATABASE_TESTS)
        docker = mock.patch("steps.mutation_core.shutil.which", return_value="/usr/bin/docker")
        docker.start()
        self.addCleanup(docker.stop)

    def run_watched(self, runner: Watched) -> None:
        context = Context(
            self.root, self.root / OUTPUT_DIRECTORY, runner, run_for=runner, module=MODULE
        )
        mutation.run(context)

    def watched(self) -> Watched:
        return Watched(healthy().stryker)

    def removals(self, runner: Watched) -> list[list[str]]:
        return [c for c in runner.commands if c[:1] == [mutation_core.DOCKER]]

    def test_stryker_runs_with_a_token_that_asks_the_tests_to_reuse_the_database(self) -> None:
        runner = self.watched()

        self.run_watched(runner)

        stryker = runner.seen[runner.commands.index(runner.called("stryker")[0])]
        self.assertRegex(stryker or "", r"^[0-9a-f]{16}$")

    def test_the_container_and_the_image_of_that_token_are_removed_at_the_end(self) -> None:
        runner = self.watched()

        self.run_watched(runner)

        token = runner.seen[runner.commands.index(runner.called("stryker")[0])]
        self.assertEqual(
            self.removals(runner),
            [
                ["docker", "rm", "--force", "--volumes", f"cdg-test-database-{token}"],
                ["docker", "rmi", "--force", f"cdg-test-database:{token}"],
            ],
        )
        self.assertEqual(runner.commands[-2:], self.removals(runner))

    def test_they_are_removed_when_stryker_fails_or_runs_out_of_time(self) -> None:
        runner = self.watched()
        runner.failing = ("stryker",)

        self.run_watched(runner)

        self.assertEqual(len(self.removals(runner)), 2)

    def test_they_are_removed_when_the_run_is_interrupted(self) -> None:
        runner = Watched(healthy().stryker, raising=KeyboardInterrupt())

        with self.assertRaises(KeyboardInterrupt):
            self.run_watched(runner)

        self.assertEqual(len(self.removals(runner)), 2)

    def test_a_failure_to_remove_them_is_not_a_finding(self) -> None:
        runner = self.watched()
        runner.failing = ("docker",)

        self.run_watched(runner)

        self.assertEqual(len(self.removals(runner)), 2)

    def test_the_variable_does_not_outlive_the_run(self) -> None:
        self.run_watched(self.watched())

        self.assertNotIn(VARIABLE, os.environ)

    def test_a_variable_the_caller_had_is_put_back(self) -> None:
        with mock.patch.dict(os.environ, {VARIABLE: "caller"}):
            self.run_watched(self.watched())

            self.assertEqual(os.environ[VARIABLE], "caller")

    def test_every_run_has_a_token_of_its_own(self) -> None:
        first, second = self.watched(), self.watched()

        self.run_watched(first)
        self.run_watched(second)

        tokens = {r.seen[r.commands.index(r.called("stryker")[0])] for r in (first, second)}
        self.assertEqual(len(tokens), 2)

    def test_tests_that_start_no_database_are_left_as_they_were(self) -> None:
        support.write(self.root, PROJECT, REFERENCE)
        runner = self.watched()

        self.run_watched(runner)

        self.assertEqual((set(runner.seen), self.removals(runner)), ({None}, []))

    def test_without_the_docker_cli_nothing_is_reused(self) -> None:
        runner = self.watched()

        with mock.patch("steps.mutation_core.shutil.which", return_value=None):
            self.run_watched(runner)

        self.assertEqual((set(runner.seen), self.removals(runner)), ({None}, []))
