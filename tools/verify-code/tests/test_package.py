"""Tests for the package step, with a fake runner in place of git and dotnet."""

from __future__ import annotations

import tempfile
import unittest
from collections.abc import Sequence
from dataclasses import dataclass, field
from pathlib import Path

from steps import package
from steps.common import OUTPUT_DIRECTORY, CommandOutcome, Context, StepResult, run_process

from . import support

COMMIT = "a" * 40
OTHER_COMMIT = "b" * 40
ORIGIN = "https://example.invalid/zqx/ZQX-MARKER-4410.git"
CLEAN_IMAGE = package.stamp("BuildInfo.Commit", COMMIT) + package.stamp(*package.CLEAN_TREE)
DIRTY_IMAGE = package.stamp("BuildInfo.Commit", COMMIT) + package.stamp(
    "BuildInfo.WorkingTree", "Dirty"
)
FAILURE = CommandOutcome(False, "ZQX-MARKER-5521 failed")
REMOTE = CommandOutcome(True, ORIGIN + "\n")


@dataclass
class PublishingRunner:
    """Answers git and dotnet as a clean copy would; each publish writes the next set of files."""

    publications: list[dict[str, bytes]]
    status: str = ""
    failing: str = ""
    origin: CommandOutcome = REMOTE
    commands: list[list[str]] = field(default_factory=list[list[str]])

    def __call__(self, command: Sequence[str], cwd: Path) -> CommandOutcome:
        self.commands.append(list(command))
        if self.failing in command:
            return FAILURE
        answers = {
            "rev-parse": CommandOutcome(True, COMMIT + "\n"),
            "get-url": self.origin,
            "status": CommandOutcome(True, self.status),
        }
        if "publish" in command:
            self.write(Path(command[command.index("--output") + 1]))
        return next((answers[word] for word in command if word in answers), support.SUCCESS)

    def containing(self, word: str) -> list[list[str]]:
        """Every command recorded so far that has ``word`` as one of its arguments."""
        return [command for command in self.commands if word in command]

    def write(self, output: Path) -> None:
        for name, content in self.publications.pop(0).items():
            target = output / name
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(content)


@dataclass
class RealGitRunner:
    """Runs git for real; each publish records what the copy holds and writes a clean host."""

    commit: str
    seen: list[dict[str, str]] = field(default_factory=list[dict[str, str]])

    def __call__(self, command: Sequence[str], cwd: Path) -> CommandOutcome:
        if command[0] == "git":
            return run_process(command, cwd)
        if "publish" in command:
            self.seen.append({p.name: p.read_text() for p in cwd.iterdir() if p.is_file()})
            image = package.stamp("BuildInfo.Commit", self.commit) + package.stamp(
                *package.CLEAN_TREE
            )
            output = Path(command[command.index("--output") + 1])
            output.mkdir(parents=True)
            (output / package.HOST_ASSEMBLY).write_bytes(image)
        return support.SUCCESS


def publication(name: str = "", content: bytes = b"") -> dict[str, bytes]:
    """A clean host and its dependency file, plus ``name`` with ``content`` when one is given."""
    files = {package.HOST_ASSEMBLY: CLEAN_IMAGE, "Core.Host.deps.json": b"{}"}
    return files | ({name: content} if name else {})


class PackageTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)
        support.write(self.root, package.PROJECT, "<Project />\n")

    def run_step(self, runner: PublishingRunner) -> StepResult:
        return package.run(Context(self.root, self.root / OUTPUT_DIRECTORY, runner))

    def test_identical_publications_pass_counting_the_files(self) -> None:
        runner = PublishingRunner([publication(), publication()])

        result = self.run_step(runner)

        self.assertTrue(result.ok, result.details)
        self.assertIn("2 arquivos publicados iguais byte a byte", result.details[0])

    def test_a_file_only_the_second_build_published_fails_naming_its_relative_path(self) -> None:
        runner = PublishingRunner([publication(), publication("runtimes/host.json", b"{1}")])

        result = self.run_step(runner)

        self.assertFalse(result.ok)
        self.assertIn("diferem em 1 de 3 arquivos", result.details[0])
        self.assertEqual(result.details[1], "runtimes/host.json: só na segunda construção")

    def test_a_changed_file_fails_saying_its_content_differs(self) -> None:
        changed = publication("Core.Host.deps.json", b"{ }")
        runner = PublishingRunner([publication(), changed])

        result = self.run_step(runner)

        self.assertFalse(result.ok)
        self.assertEqual(
            result.details[1], "Core.Host.deps.json: conteúdo difere entre as duas construções"
        )

    def test_a_file_only_the_first_build_published_is_named(self) -> None:
        runner = PublishingRunner([publication("extra.txt", b"x"), publication()])

        result = self.run_step(runner)

        self.assertEqual(result.details[1], "extra.txt: só na primeira construção")

    def test_the_comparison_never_prints_a_machine_path(self) -> None:
        runner = PublishingRunner([publication(), publication("runtimes/host.json", b"{1}")])

        result = self.run_step(runner)

        self.assertNotIn(self._tmp.name, "\n".join(result.details))
        self.assertNotIn(tempfile.gettempdir(), "\n".join(result.details))

    def test_a_failed_publish_fails_and_shows_its_output(self) -> None:
        runner = PublishingRunner([publication(), publication()], failing="publish")

        result = self.run_step(runner)

        self.assertFalse(result.ok)
        self.assertIn("publicação do núcleo falhou", result.details[0])
        self.assertIn("  | ZQX-MARKER-5521 failed", result.details)

    def test_a_failed_locked_restore_fails_naming_the_lock(self) -> None:
        runner = PublishingRunner([publication(), publication()], failing="restore")

        result = self.run_step(runner)

        self.assertFalse(result.ok)
        self.assertEqual(result.details[0], package.LOCK_FAILED)

    def test_a_copy_git_does_not_see_as_clean_fails_before_any_build(self) -> None:
        runner = PublishingRunner([publication(), publication()], status="?? ZQX-STRAY.txt\n")

        result = self.run_step(runner)

        self.assertFalse(result.ok)
        self.assertEqual(result.details[0], package.NOT_CLEAN)
        self.assertEqual(runner.containing("restore"), [])

    def test_a_host_stamped_with_a_dirty_tree_fails(self) -> None:
        dirty = publication(package.HOST_ASSEMBLY, DIRTY_IMAGE)
        runner = PublishingRunner([dirty, publication()])

        result = self.run_step(runner)

        self.assertFalse(result.ok)
        self.assertIn("não declara BuildInfo.WorkingTree=Clean", result.details[0])

    def test_a_host_stamped_with_another_commit_fails(self) -> None:
        image = package.stamp("BuildInfo.Commit", OTHER_COMMIT) + package.stamp(*package.CLEAN_TREE)
        runner = PublishingRunner([publication(package.HOST_ASSEMBLY, image), publication()])

        result = self.run_step(runner)

        self.assertFalse(result.ok)
        self.assertIn(f"não declara BuildInfo.Commit={COMMIT}", result.details[0])

    def test_a_missing_host_assembly_fails(self) -> None:
        runner = PublishingRunner([{"Core.Host.deps.json": b"{}"}, publication()])

        result = self.run_step(runner)

        self.assertFalse(result.ok)
        self.assertIn(package.HOST_ASSEMBLY, result.details[0])

    def test_a_git_status_that_fails_in_the_copy_fails_as_not_clean(self) -> None:
        runner = PublishingRunner([publication(), publication()], failing="status")

        result = self.run_step(runner)

        self.assertFalse(result.ok)
        self.assertEqual(result.details[0], package.NOT_CLEAN)

    def test_the_stamp_is_the_serialized_metadata_pair_of_the_compiler(self) -> None:
        stamp = package.stamp(*package.CLEAN_TREE)

        self.assertEqual(stamp, b"\x15BuildInfo.WorkingTree\x05Clean")

    def test_unreadable_head_fails_without_cloning(self) -> None:
        runner = PublishingRunner([], failing="rev-parse")

        result = self.run_step(runner)

        self.assertFalse(result.ok)
        self.assertEqual(len(runner.commands), 1)

    def test_missing_project_fails(self) -> None:
        (self.root / package.PROJECT).unlink()

        result = self.run_step(PublishingRunner([]))

        self.assertFalse(result.ok)
        self.assertIn("não há o que publicar", result.details[0])

    def test_each_copy_is_a_clone_of_the_repository_checked_out_at_head(self) -> None:
        runner = PublishingRunner([publication(), publication()])

        self.run_step(runner)

        clone = runner.commands[2]
        self.assertEqual(clone[:2], ["git", "clone"])
        self.assertEqual(clone[-2], str(self.root))
        self.assertEqual(runner.commands[3][-4:], ["checkout", "--quiet", "--detach", COMMIT])

    def test_each_copy_keeps_the_remote_of_the_working_tree_for_source_link(self) -> None:
        runner = PublishingRunner([publication(), publication()])

        self.run_step(runner)

        self.assertEqual(runner.commands[4][-3:], ["set-url", "origin", ORIGIN])

    def test_a_repository_without_origin_still_builds(self) -> None:
        runner = PublishingRunner([publication(), publication()], origin=FAILURE)

        result = self.run_step(runner)

        self.assertTrue(result.ok, result.details)
        self.assertEqual(runner.containing("set-url"), [])

    def test_both_copies_restore_locked_and_publish_as_continuous_integration(self) -> None:
        runner = PublishingRunner([publication(), publication()])

        self.run_step(runner)

        restores, publishes = runner.containing("restore"), runner.containing("publish")
        self.assertEqual((len(restores), len(publishes)), (2, 2))
        self.assertIn("--locked-mode", restores[0])
        self.assertIn("-p:ContinuousIntegrationBuild=true", publishes[1])
        self.assertIn("--no-restore", publishes[1])
        self.assertIn("-nodeReuse:false", publishes[1])

    def test_the_two_copies_live_in_different_folders(self) -> None:
        runner = PublishingRunner([publication(), publication()])

        self.run_step(runner)

        first, second = runner.containing("clone")
        self.assertNotEqual(first[-1], second[-1])

    def test_the_copies_are_deleted_after_the_step(self) -> None:
        runner = PublishingRunner([publication(), publication()])

        self.run_step(runner)

        output = runner.containing("publish")[0][-1]
        self.assertFalse(Path(output).parent.exists())


class PackageFromRealRepositoryTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.repo = support.git_repository(Path(self._tmp.name))
        support.write(self.repo, package.PROJECT, "<Project />\n")
        support.git(self.repo, "add", ".")
        support.git(self.repo, "commit", "-q", "-m", "project")

    def test_changes_not_committed_never_reach_the_copies(self) -> None:
        support.write(self.repo, "README.md", "ZQX-MARKER-6612 edited\n")
        support.write(self.repo, "ZQX-UNTRACKED.txt", "ZQX-MARKER-6613\n")
        head = run_process(["git", "rev-parse", "HEAD"], self.repo).text.strip()
        runner = RealGitRunner(head)

        result = package.run(Context(self.repo, self.repo / OUTPUT_DIRECTORY, runner))

        self.assertTrue(result.ok, result.details)
        self.assertEqual(runner.seen, [{"README.md": "base\n"}] * 2)


if __name__ == "__main__":
    unittest.main()
