"""Tests for the name of the test database image and for who uses it: the tag is the hash of the
files that define the image, and every job that starts a database receives it (#805)."""

from __future__ import annotations

import contextlib
import io
import tempfile
import unittest
from pathlib import Path
from typing import Any, cast

import database_image
import yaml

from . import support

GITHUB = support.VERIFY_CODE.parents[1] / ".github"
ACTION = GITHUB / "actions" / "build-database-image" / "action.yml"
USES_ACTION = "./.github/actions/build-database-image"
DATABASE_JOBS = ("dotnet", "properties", "browser", "mutation_module", "database_image")
PUBLISHER = "database_image"
REPOSITORY = "Owner/Repo"


def load(path: Path) -> dict[str, Any]:
    return cast("dict[str, Any]", yaml.safe_load(path.read_text(encoding="utf-8")))


def definition(root: Path, files: dict[str, str]) -> None:
    for name, text in files.items():
        target = root / database_image.DEFINITION / name
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(text, encoding="utf-8", newline="")


class ImageNameTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)
        definition(self.root, {"Dockerfile": "FROM x\n", "init/01.sh": "echo a\n"})

    def name(self) -> str:
        return database_image.image_name(self.root, REPOSITORY)

    def test_the_name_is_the_registry_path_of_the_repository_in_lower_case_and_the_hash(
        self,
    ) -> None:
        registry, _, tail = self.name().partition("/")

        self.assertEqual(registry, "ghcr.io")
        self.assertRegex(tail, r"^owner/repo/cdg-test-database:[0-9a-f]{16}$")

    def test_the_same_files_give_the_same_tag(self) -> None:
        first = self.name()

        definition(self.root, {"Dockerfile": "FROM x\n"})

        self.assertEqual(self.name(), first)

    def test_a_changed_file_gives_another_tag(self) -> None:
        first = self.name()

        definition(self.root, {"init/01.sh": "echo b\n"})

        self.assertNotEqual(self.name(), first)

    def test_a_new_file_gives_another_tag(self) -> None:
        first = self.name()

        definition(self.root, {"init/02.sh": "echo c\n"})

        self.assertNotEqual(self.name(), first)

    def test_a_moved_content_gives_another_tag(self) -> None:
        first = self.name()
        definition(self.root, {"init/02.sh": "echo a\n"})
        (self.root / database_image.DEFINITION / "init" / "01.sh").unlink()

        self.assertNotEqual(self.name(), first)

    def test_a_file_the_image_does_not_copy_does_not_change_the_tag(self) -> None:
        first = self.name()

        definition(self.root, {"compose.yml": "services: {}\n"})

        self.assertEqual(self.name(), first)

    def test_the_line_ending_of_the_checkout_does_not_change_the_tag(self) -> None:
        first = self.name()

        definition(self.root, {"Dockerfile": "FROM x\r\n", "init/01.sh": "echo a\r\n"})

        self.assertEqual(self.name(), first)

    def test_the_real_definition_has_a_name(self) -> None:
        root = support.VERIFY_CODE.parents[1]

        self.assertRegex(database_image.image_name(root, REPOSITORY), r":[0-9a-f]{16}$")


class WorkflowTests(unittest.TestCase):
    def setUp(self) -> None:
        self.workflow = load(GITHUB / "workflows" / "verify-code.yml")
        self.jobs = cast("dict[str, Any]", self.workflow["jobs"])

    def steps(self, job: str) -> list[dict[str, Any]]:
        return cast("list[dict[str, Any]]", self.jobs[job]["steps"])

    def uses_the_action(self, job: str) -> bool:
        return any(step.get("uses") == USES_ACTION for step in self.steps(job))

    def test_no_job_gets_the_image_that_is_not_listed(self) -> None:
        users = {name for name in self.jobs if self.uses_the_action(name)}

        self.assertEqual(users, set(DATABASE_JOBS))

    def test_the_mutation_matrix_gets_the_image_only_for_the_core_modules(self) -> None:
        step = next(s for s in self.steps("mutation_module") if s.get("uses") == USES_ACTION)

        self.assertEqual(step["if"], "startsWith(matrix.module, 'Core.')")

    def test_the_matrix_waits_for_the_image_and_does_not_fall_with_it(self) -> None:
        job = self.jobs["mutation_module"]

        self.assertIn(PUBLISHER, job["needs"])
        self.assertIn("!cancelled()", job["if"])
        self.assertIn("needs.mutation_modules.result == 'success'", job["if"])

    def test_a_job_that_runs_code_of_a_pull_request_only_reads_packages(self) -> None:
        for job in DATABASE_JOBS:
            if job != PUBLISHER:
                with self.subTest(job=job):
                    self.assertEqual(self.jobs[job]["permissions"]["packages"], "read")

    def test_only_the_job_of_main_writes_packages_and_publishes(self) -> None:
        writers = [
            name
            for name, job in self.jobs.items()
            if job.get("permissions", {}).get("packages") == "write"
        ]
        publishers = [
            name
            for name in self.jobs
            if any(step.get("with", {}).get("publish") == "true" for step in self.steps(name))
        ]

        self.assertEqual(writers, [PUBLISHER])
        self.assertEqual(publishers, [PUBLISHER])

    def test_the_job_that_publishes_never_runs_for_a_pull_request_or_a_branch(self) -> None:
        condition = self.jobs[PUBLISHER]["if"]

        self.assertIn("github.event_name != 'pull_request'", condition)
        self.assertIn("github.ref == 'refs/heads/main'", condition)

    def test_the_job_that_publishes_does_not_leave_the_token_in_the_checkout(self) -> None:
        checkout = self.steps(PUBLISHER)[0]

        self.assertIs(checkout["with"]["persist-credentials"], False)

    def test_the_remote_build_reads_packages_and_never_publishes(self) -> None:
        build = load(GITHUB / "workflows" / "build-remoto.yml")["jobs"]["build"]
        action = next(s for s in build["steps"] if s.get("uses") == USES_ACTION)

        self.assertEqual(build["permissions"]["packages"], "read")
        self.assertNotIn("publish", action.get("with", {}))


class ActionTests(unittest.TestCase):
    def test_it_runs_the_provider_with_the_token_and_the_permission_to_publish(self) -> None:
        action = load(ACTION)
        step = action["runs"]["steps"][0]

        self.assertEqual(
            step["run"],
            'python3 tools/verify-code/database_image.py --provide "$GITHUB_REPOSITORY"',
        )
        self.assertEqual(
            step["env"], {"TOKEN": "${{ inputs.token }}", "PUBLISH": "${{ inputs.publish }}"}
        )
        self.assertEqual(action["inputs"]["publish"]["default"], "false")


class FakeDocker:
    """Answers the commands of the provider and keeps them, so the branches can be told apart."""

    def __init__(self, failing: dict[str, tuple[int, str]] | None = None) -> None:
        self.failing = failing or {}
        self.calls: list[list[str]] = []

    def __call__(self, args: list[str], stdin: str | None = None) -> tuple[int, str]:
        self.calls.append(args)
        return self.failing.get(self.verb(args), (0, ""))

    @staticmethod
    def verb(call: list[str]) -> str:
        return "build" if "build" in call else call[1]

    def verbs(self) -> list[str]:
        return [self.verb(call) for call in self.calls]


class ProvideTests(unittest.TestCase):
    IMAGE = "ghcr.io/o/r/cdg-test-database:abc"

    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.env_file = Path(self._tmp.name) / "env"
        self.summary = Path(self._tmp.name) / "summary"

    def job(self, *, token: str = "t", publish: bool = False) -> database_image.Provision:
        return database_image.Provision(
            image=self.IMAGE,
            token=token,
            actor="me",
            publish=publish,
            source="https://github.com/o/r",
            env_file=self.env_file,
            summary=self.summary,
        )

    def provide(self, docker: FakeDocker, job: database_image.Provision) -> tuple[int, str]:
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            code = database_image.provide(job, docker)
        return code, out.getvalue()

    def exported(self) -> str:
        return self.env_file.read_text(encoding="utf-8") if self.env_file.exists() else ""

    def test_an_image_that_exists_is_pulled_and_never_built(self) -> None:
        docker = FakeDocker()

        code, _ = self.provide(docker, self.job(publish=True))

        self.assertEqual(code, 0)
        self.assertEqual(docker.verbs(), ["login", "pull", "logout"])
        self.assertEqual(self.exported(), f"TEST_DATABASE_IMAGE={self.IMAGE}\n")

    def test_an_image_that_does_not_exist_is_built_with_its_tag_and_published_by_main(self) -> None:
        docker = FakeDocker({"pull": (1, "Error: manifest unknown")})

        code, out = self.provide(docker, self.job(publish=True))

        build = next(call for call in docker.calls if "build" in call)
        self.assertEqual(code, 0)
        self.assertEqual(docker.verbs(), ["login", "pull", "build", "push", "logout"])
        self.assertEqual(build[build.index("--tag") + 1], self.IMAGE)
        self.assertNotIn("::notice", out)
        self.assertEqual(self.exported(), f"TEST_DATABASE_IMAGE={self.IMAGE}\n")

    def test_a_run_that_may_not_publish_builds_and_does_not_push(self) -> None:
        docker = FakeDocker({"pull": (1, "manifest unknown")})

        code, _ = self.provide(docker, self.job(publish=False))

        self.assertEqual(code, 0)
        self.assertEqual(docker.verbs(), ["login", "pull", "build", "logout"])
        self.assertIn("TEST_DATABASE_IMAGE=", self.exported())

    def test_without_a_token_it_never_logs_in_and_builds_locally(self) -> None:
        docker = FakeDocker()

        code, _ = self.provide(docker, self.job(token="", publish=True))

        self.assertEqual(code, 0)
        self.assertEqual(docker.verbs(), ["build"])
        self.assertIn("TEST_DATABASE_IMAGE=", self.exported())

    def test_a_refused_login_builds_locally_and_does_not_push(self) -> None:
        docker = FakeDocker({"login": (1, "denied")})

        code, _ = self.provide(docker, self.job(publish=True))

        self.assertEqual(code, 0)
        self.assertEqual(docker.verbs(), ["login", "build"])

    def test_a_refused_push_is_a_warning_and_the_image_built_here_is_used(self) -> None:
        docker = FakeDocker({"pull": (1, "manifest unknown"), "push": (1, "denied")})

        code, out = self.provide(docker, self.job(publish=True))

        self.assertEqual(code, 0)
        self.assertIn("::warning::", out)
        self.assertIn("TEST_DATABASE_IMAGE=", self.exported())

    def test_a_pull_that_fails_for_another_reason_says_so_before_it_builds(self) -> None:
        docker = FakeDocker({"pull": (1, "toomanyrequests: rate limit")})

        _, out = self.provide(docker, self.job())

        self.assertIn("::notice::", out)
        self.assertIn("rate limit", out)

    def test_a_build_that_fails_fails_the_step_and_names_the_apt_lines_once(self) -> None:
        apt = "#7 3.1 E: Unable to locate package x\n#7 3.2 E: Unable to locate package x\nother\n"
        docker = FakeDocker({"pull": (1, "manifest unknown"), "build": (100, apt)})

        code, _ = self.provide(docker, self.job(publish=True))

        summary = self.summary.read_text(encoding="utf-8")
        self.assertEqual(code, 1)
        self.assertEqual(docker.verbs(), ["login", "pull", "build", "logout"])
        self.assertEqual(self.exported(), "")
        self.assertEqual(summary.count("Unable to locate package x"), 1)

    def test_a_build_that_fails_without_an_apt_line_says_where_the_cause_is(self) -> None:
        docker = FakeDocker({"build": (1, "boom\n")})

        self.provide(docker, self.job(token=""))

        self.assertIn("a causa está no fim do log do build", self.summary.read_text("utf-8"))


class MainTests(unittest.TestCase):
    def run_main(self, arguments: list[str]) -> tuple[int, str]:
        out = io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(io.StringIO()):
            return database_image.main(arguments), out.getvalue()

    def test_a_name_without_an_owner_is_a_usage_error(self) -> None:
        self.assertEqual(self.run_main(["repository"])[0], database_image.USAGE)
        self.assertEqual(self.run_main([])[0], database_image.USAGE)
        self.assertEqual(self.run_main(["--provide"])[0], database_image.USAGE)

    def test_the_name_of_the_repository_prints_the_name_of_the_image(self) -> None:
        code, out = self.run_main(["Owner/Repo"])

        self.assertEqual(code, 0)
        self.assertRegex(out, r"^ghcr\.io/owner/repo/cdg-test-database:[0-9a-f]{16}\n$")


if __name__ == "__main__":
    unittest.main()
