"""Tests for the apt of the test images: it retries, it carries no version a repository can drop,
and when it fails the ``E:`` line it wrote reaches the report and the job summary (#733)."""

from __future__ import annotations

import os
import re
import tempfile
import unittest
from collections.abc import Sequence
from pathlib import Path
from unittest import mock

from steps import apt_errors, coverage_by_area, offline_containers, offline_start, package
from steps.common import OUTPUT_DIRECTORY, CommandOutcome, Context, StepResult

from . import support

REPOSITORY = support.VERIFY_CODE.parents[1]
DOCKERFILES = (
    REPOSITORY / "tools" / "database" / "Dockerfile",
    support.VERIFY_CODE / "offline_start" / "build.Dockerfile",
    support.VERIFY_CODE / "offline_start" / "start.Dockerfile",
)
DATABASE_ACTION = REPOSITORY / ".github" / "actions" / "build-database-image" / "action.yml"
RETRIES = "-o Acquire::Retries=3"
UNABLE = "E: Unable to locate package strace"
FAILED_FETCH = (
    "E: Failed to fetch http://archive.ubuntu.com/pool/s/strace.deb  503  Service Unavailable"
)
BUILD_OUTPUT = (
    "#7 [3/5] RUN set -eux; apt-get update; apt-get install strace\n"
    "#7 0.4 Reading package lists...\n"
    f"#7 5.12 {UNABLE}\n"
    f"#7 5.13 {FAILED_FETCH}\n"
    f"#7 5.14 {UNABLE}\n"
    '#7 ERROR: process "/bin/sh -c apt-get install strace" did not complete: exit code: 100\n'
    "ERROR: failed to solve: process did not complete successfully\n"
)


def apt_lines(path: Path) -> list[str]:
    """The lines of the Dockerfile that call apt-get, with their continuation lines joined."""
    text = path.read_text(encoding="utf-8").replace("\\n", " ")
    return [line.strip() for line in text.splitlines() if "apt-get" in line]


class AptOfTheImagesTests(unittest.TestCase):
    def test_every_apt_get_of_every_test_image_retries_a_small_number_of_times(self) -> None:
        for path in DOCKERFILES:
            for command in re.findall(r"apt-get [^;]+", " ".join(apt_lines(path))):
                with self.subTest(file=path.name, command=command):
                    self.assertIn(RETRIES, command)

    def test_the_test_images_have_the_apt_calls_this_test_covers(self) -> None:
        for path in DOCKERFILES:
            with self.subTest(file=path.name):
                self.assertTrue(apt_lines(path))

    def test_the_database_image_installs_pgaudit_without_a_version_the_repository_can_drop(
        self,
    ) -> None:
        text = DOCKERFILES[0].read_text(encoding="utf-8").replace("\\n", " ")

        installs = [line for line in text.splitlines() if "apt-get" in line and " install " in line]

        self.assertEqual(len(installs), 1)
        self.assertIn("postgresql-17-pgaudit", installs[0])
        self.assertNotIn("pgaudit=", installs[0].replace(RETRIES, ""))

    def test_the_database_image_prints_the_version_of_pgaudit_it_installed(self) -> None:
        text = DOCKERFILES[0].read_text(encoding="utf-8")

        self.assertRegex(text, r"dpkg-query -W -f='[^']*\$\{Version\}[^']*' postgresql-17-pgaudit")

    def test_no_test_image_pins_an_apt_package_to_a_version(self) -> None:
        for path in DOCKERFILES:
            for command in re.findall(r"apt-get [^;]+", " ".join(apt_lines(path))):
                with self.subTest(file=path.name, command=command):
                    self.assertNotRegex(command.replace(RETRIES, ""), r"\S=\S")


class AptErrorsTests(unittest.TestCase):
    def test_the_e_lines_come_once_each_without_the_step_and_the_second(self) -> None:
        self.assertEqual(apt_errors.apt_errors(BUILD_OUTPUT), (UNABLE, FAILED_FETCH))

    def test_a_plain_e_line_without_the_progress_prefix_counts(self) -> None:
        self.assertEqual(apt_errors.apt_errors(f"Reading...\n{UNABLE}\n"), (UNABLE,))

    def test_an_output_without_one_gives_none(self) -> None:
        self.assertEqual(apt_errors.apt_errors("ERROR: failed to solve\nW: a warning\n"), ())

    def test_the_summary_lists_the_lines_in_a_block_under_the_title(self) -> None:
        text = apt_errors.summary("Título", (UNABLE,))

        self.assertEqual(text, f"### Título\n\n```\n{UNABLE}\n```\n")

    def test_a_summary_with_no_line_says_the_apt_wrote_none(self) -> None:
        self.assertIn(apt_errors.NONE_FOUND, apt_errors.summary("Título", ()))


class FailedBuildReportTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)
        support.write(self.root, package.PROJECT, "<Project />\n")
        environment = mock.patch.dict(os.environ, clear=False)
        environment.start()
        self.addCleanup(environment.stop)
        self.summary = self.root / "summary.md"
        os.environ[coverage_by_area.SUMMARY] = str(self.summary)

    def failing_build(self, output: str) -> StepResult:
        def runner(command: Sequence[str], cwd: Path) -> CommandOutcome:
            _ = cwd
            is_build = list(command)[:2] == ["docker", "build"]
            return CommandOutcome(False, output, 1) if is_build else support.SUCCESS

        context = Context(self.root, self.root / OUTPUT_DIRECTORY, runner)
        return offline_start.run(context)

    def test_a_failed_build_shows_the_e_lines_before_the_tail_of_the_output(self) -> None:
        result = self.failing_build(BUILD_OUTPUT)

        self.assertFalse(result.ok)
        self.assertLess(
            result.details.index(f"  apt: {UNABLE}"), result.details.index(f"  | #7 5.12 {UNABLE}")
        )

    def test_a_failed_build_writes_the_e_lines_in_the_job_summary(self) -> None:
        _ = self.failing_build(BUILD_OUTPUT)

        text = self.summary.read_text(encoding="utf-8")

        self.assertIn(UNABLE, text)
        self.assertIn(FAILED_FETCH, text)

    def test_the_images_are_built_with_the_plain_progress_so_the_apt_lines_are_in_the_output(
        self,
    ) -> None:
        for image in (
            offline_containers.BUILDER,
            offline_containers.RUNNER,
            offline_containers.SERVER,
        ):
            with self.subTest(image=image.tag):
                self.assertIn("--progress=plain", image.command())
                self.assertNotIn("--quiet", image.command())


class DatabaseActionTests(unittest.TestCase):
    def test_a_failed_build_puts_the_e_lines_in_the_job_summary_and_fails_the_step(self) -> None:
        text = DATABASE_ACTION.read_text(encoding="utf-8")

        self.assertIn("set -o pipefail", text)
        self.assertIn("GITHUB_STEP_SUMMARY", text)
        self.assertIn("E: ", text)
        self.assertIn("exit 1", text)


if __name__ == "__main__":
    unittest.main()
