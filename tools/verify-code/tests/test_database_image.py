"""Tests for the name of the test database image and for who uses it: the tag is the hash of the
files that define the image, and every job that starts a database receives it (#805)."""

from __future__ import annotations

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

    def uses_the_action(self, job: str) -> bool:
        steps = cast("list[dict[str, Any]]", self.jobs[job]["steps"])
        return any(step.get("uses") == USES_ACTION for step in steps)

    def test_every_job_that_starts_a_database_uses_the_action_that_gives_the_image(self) -> None:
        for job in DATABASE_JOBS:
            with self.subTest(job=job):
                self.assertTrue(self.uses_the_action(job))

    def test_the_mutation_matrix_gets_the_image_only_for_the_core_modules(self) -> None:
        steps = cast("list[dict[str, Any]]", self.jobs["mutation_module"]["steps"])
        step = next(s for s in steps if s.get("uses") == USES_ACTION)

        self.assertEqual(step["if"], "startsWith(matrix.module, 'Core.')")

    def test_the_matrix_waits_for_the_job_that_publishes_the_image_once(self) -> None:
        self.assertIn("database_image", self.jobs["mutation_module"]["needs"])

    def test_the_jobs_that_pull_or_push_the_image_may_write_packages(self) -> None:
        for job in DATABASE_JOBS:
            with self.subTest(job=job):
                self.assertEqual(self.jobs[job]["permissions"]["packages"], "write")
                self.assertEqual(self.jobs[job]["permissions"]["contents"], "read")


class ActionTests(unittest.TestCase):
    def setUp(self) -> None:
        self.text = ACTION.read_text(encoding="utf-8")

    def test_it_pulls_the_image_of_the_content_before_building_it(self) -> None:
        self.assertIn("database_image.py", self.text)
        self.assertLess(self.text.index("docker pull"), self.text.index("docker build"))

    def test_it_publishes_what_it_built_and_a_refused_push_is_only_a_warning(self) -> None:
        self.assertRegex(self.text, r"docker push [^\n]*\|\| echo '::warning")

    def test_without_a_login_it_builds_locally_and_does_not_push(self) -> None:
        self.assertIn('if [ "$logged" = true ]', self.text)

    def test_it_hands_the_name_of_the_content_to_the_tests(self) -> None:
        self.assertIn('echo "TEST_DATABASE_IMAGE=$image" >> "$GITHUB_ENV"', self.text)


if __name__ == "__main__":
    unittest.main()
