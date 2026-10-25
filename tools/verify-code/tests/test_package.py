"""Tests for the package step, with a fake runner in place of git and dotnet."""

from __future__ import annotations

import sys
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
LINUX, WINDOWS = package.TARGETS
ORIGIN = "https://example.invalid/zqx/ZQX-MARKER-4410.git"
FAILURE = CommandOutcome(False, "ZQX-MARKER-5521 failed")
REMOTE = CommandOutcome(True, ORIGIN + "\n")
FOREIGN = package.FOREIGN_TARGET
REFUSED = CommandOutcome(
    False,
    f"  - RuntimeIdentifier: publicado para o alvo {FOREIGN}, mas o destino em que roda é zqx-x64",
    69,
)


def image(target: str, commit: str = COMMIT, tree: str = "Clean") -> bytes:
    """An executable carrying the runtime, stamped as a build of ``commit`` for ``target``."""
    return (
        package.stamp("BuildInfo.Commit", commit)
        + package.stamp("BuildInfo.WorkingTree", tree)
        + package.stamp("BuildInfo.RuntimeIdentifier", target)
        + package.RUNTIME_INSIDE
    )


@dataclass
class PublishingRunner:
    """Answers git and dotnet as a clean copy would; each publish writes the next set of files."""

    publications: list[dict[str, bytes]]
    status: str = ""
    failing: str = ""
    origin: CommandOutcome = REMOTE
    started: CommandOutcome = REFUSED
    commands: list[list[str]] = field(default_factory=list[list[str]])

    def __call__(self, command: Sequence[str], cwd: Path) -> CommandOutcome:
        self.commands.append(list(command))
        if self.failing in command:
            return FAILURE
        answers = {
            "rev-parse": CommandOutcome(True, COMMIT + "\n"),
            "get-url": self.origin,
            "status": CommandOutcome(True, self.status),
            package.UNKNOWN_KEY: self.started,
        }
        if "publish" in command:
            self.write(Path(command[command.index("--output") + 1]))
        return next((answers[word] for word in command if word in answers), support.SUCCESS)

    def containing(self, word: str) -> list[list[str]]:
        """Every command recorded so far that has ``word`` as one of its arguments."""
        return [command for command in self.commands if word in command]

    def write(self, output: Path) -> None:
        output.mkdir(parents=True, exist_ok=True)
        for name, content in self.publications.pop(0).items():
            (output / name).write_bytes(content)


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
            target = command[command.index("--runtime") + 1]
            output = Path(command[command.index("--output") + 1])
            output.mkdir(parents=True)
            (output / package.executable(target)).write_bytes(image(target, self.commit))
            (output / package.SETTINGS_FILE).write_bytes(b"{}")
        return REFUSED if package.UNKNOWN_KEY in command else support.SUCCESS


def publication(target: str, name: str = "", content: bytes = b"") -> dict[str, bytes]:
    """A clean executable for ``target`` and its settings, plus ``name`` with ``content``."""
    files = {package.executable(target): image(target), package.SETTINGS_FILE: b"{}"}
    return files | ({name: content} if name else {})


def builds(first_linux: dict[str, bytes] | None = None) -> list[dict[str, bytes]]:
    """What the four publishes of a run write, in order: each copy publishes Linux, then Windows."""
    linux = first_linux if first_linux is not None else publication(LINUX)
    return [linux, publication(WINDOWS), publication(LINUX), publication(WINDOWS)]


class PackageTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)
        support.write(self.root, package.PROJECT, "<Project />\n")

    def run_step(self, runner: PublishingRunner) -> StepResult:
        return package.run(Context(self.root, self.root / OUTPUT_DIRECTORY, runner))

    def test_identical_publications_pass_counting_the_files_of_both_targets(self) -> None:
        runner = PublishingRunner(builds())

        result = self.run_step(runner)

        self.assertTrue(result.ok, result.details)
        self.assertIn("4 arquivos publicados iguais byte a byte", result.details[0])

    def test_a_changed_file_fails_naming_its_target_and_saying_its_content_differs(self) -> None:
        changed = publication(LINUX, package.SETTINGS_FILE, b"{ }")
        runner = PublishingRunner(builds(changed))

        result = self.run_step(runner)

        self.assertFalse(result.ok)
        self.assertIn("diferem em 1 de 4 arquivos", result.details[0])
        self.assertEqual(
            result.details[1],
            "linux-x64/appsettings.json: conteúdo difere entre as duas construções",
        )

    def test_a_file_only_one_build_has_is_named_with_the_build(self) -> None:
        result = package.compare({"extra.txt": "1", "x": "2"}, {"x": "2", "late.txt": "3"})

        self.assertEqual(result.details[1], "extra.txt: só na primeira construção")
        self.assertEqual(result.details[2], "late.txt: só na segunda construção")

    def test_the_comparison_never_prints_a_machine_path(self) -> None:
        changed = publication(LINUX, package.SETTINGS_FILE, b"{ }")
        runner = PublishingRunner(builds(changed))

        result = self.run_step(runner)

        self.assertNotIn(self._tmp.name, "\n".join(result.details))
        self.assertNotIn(tempfile.gettempdir(), "\n".join(result.details))

    def test_a_library_beside_the_executable_fails_naming_it_and_the_target(self) -> None:
        runner = PublishingRunner(builds(publication(LINUX, "libZQX-MARKER-3317.so", b"\x7fELF")))

        result = self.run_step(runner)

        self.assertFalse(result.ok)
        self.assertEqual(result.details[0], "linux-x64: a publicação não é um executável único")
        self.assertEqual(result.details[1:], ("sobra libZQX-MARKER-3317.so",))

    def test_a_publication_without_the_executable_fails_naming_what_is_missing(self) -> None:
        runner = PublishingRunner(builds({package.SETTINGS_FILE: b"{}"}))

        result = self.run_step(runner)

        self.assertFalse(result.ok)
        self.assertEqual(result.details[1:], ("falta Core.Host",))

    def test_the_windows_target_expects_the_executable_with_its_extension(self) -> None:
        unix_named = {"Core.Host": image(WINDOWS), package.SETTINGS_FILE: b"{}"}
        runner = PublishingRunner([publication(LINUX), unix_named])

        result = self.run_step(runner)

        self.assertEqual(result.details[0], "win-x64: a publicação não é um executável único")
        self.assertEqual(result.details[1:], ("sobra Core.Host", "falta Core.Host.exe"))

    def test_an_executable_stamped_for_another_target_fails(self) -> None:
        other = publication(LINUX, package.executable(LINUX), image("ZQX-RID"))
        runner = PublishingRunner(builds(other))

        result = self.run_step(runner)

        self.assertFalse(result.ok)
        self.assertIn("não declara BuildInfo.RuntimeIdentifier=linux-x64", result.details[0])

    def test_an_executable_without_the_runtime_inside_fails(self) -> None:
        bare = image(LINUX).replace(package.RUNTIME_INSIDE, b"")
        runner = PublishingRunner(builds(publication(LINUX, package.executable(LINUX), bare)))

        result = self.run_step(runner)

        self.assertFalse(result.ok)
        self.assertEqual(
            result.details[0], "linux-x64: Core.Host não carrega a plataforma de execução"
        )

    def test_the_host_built_for_a_foreign_target_is_run_with_an_unknown_key(self) -> None:
        runner = PublishingRunner(builds())

        self.run_step(runner)

        build = runner.containing("build")[0]
        self.assertIn(f"-p:RuntimeIdentifier={FOREIGN}", build)
        self.assertIn("-p:SelfContained=false", build)
        started = runner.containing(package.UNKNOWN_KEY)[0]
        self.assertEqual(Path(started[1]).name, "Core.Host.dll")

    def test_a_foreign_host_that_reaches_the_configuration_fails(self) -> None:
        configuration = CommandOutcome(False, "a configuração foi recusada", 78)
        runner = PublishingRunner(builds(), started=configuration)

        result = self.run_step(runner)

        self.assertFalse(result.ok)
        self.assertEqual(
            result.details[0], f"o núcleo para {FOREIGN} não recusou o destino desta máquina"
        )

    def test_a_foreign_host_ending_with_the_check_code_but_another_message_fails(self) -> None:
        runner = PublishingRunner(builds(), started=CommandOutcome(False, "ZQX-MARKER-8810", 69))

        result = self.run_step(runner)

        self.assertFalse(result.ok)

    def test_a_foreign_host_naming_the_target_with_another_code_fails(self) -> None:
        runner = PublishingRunner(builds(), started=CommandOutcome(False, REFUSED.text, 1))

        result = self.run_step(runner)

        self.assertFalse(result.ok)

    def test_the_foreign_target_is_one_this_machine_is_not(self) -> None:
        self.assertIn(FOREIGN, package.TARGETS)
        self.assertNotEqual(FOREIGN.startswith("win-"), sys.platform.startswith("win"))

    def test_a_failed_publish_fails_naming_the_target_and_shows_its_output(self) -> None:
        runner = PublishingRunner(builds(), failing="publish")

        result = self.run_step(runner)

        self.assertFalse(result.ok)
        self.assertIn("publicação do núcleo para linux-x64 falhou", result.details[0])
        self.assertIn("  | ZQX-MARKER-5521 failed", result.details)

    def test_a_failed_locked_restore_fails_naming_the_lock(self) -> None:
        runner = PublishingRunner(builds(), failing="restore")

        result = self.run_step(runner)

        self.assertFalse(result.ok)
        self.assertEqual(result.details[0], package.LOCK_FAILED)

    def test_a_copy_git_does_not_see_as_clean_fails_before_any_build(self) -> None:
        runner = PublishingRunner(builds(), status="?? ZQX-STRAY.txt\n")

        result = self.run_step(runner)

        self.assertFalse(result.ok)
        self.assertEqual(result.details[0], package.NOT_CLEAN)
        self.assertEqual(runner.containing("restore"), [])

    def test_a_host_stamped_with_a_dirty_tree_fails(self) -> None:
        dirty = publication(LINUX, package.executable(LINUX), image(LINUX, tree="Dirty"))
        runner = PublishingRunner(builds(dirty))

        result = self.run_step(runner)

        self.assertFalse(result.ok)
        self.assertIn("não declara BuildInfo.WorkingTree=Clean", result.details[0])

    def test_a_host_stamped_with_another_commit_fails(self) -> None:
        other = publication(LINUX, package.executable(LINUX), image(LINUX, OTHER_COMMIT))
        runner = PublishingRunner(builds(other))

        result = self.run_step(runner)

        self.assertFalse(result.ok)
        self.assertIn(f"não declara BuildInfo.Commit={COMMIT}", result.details[0])

    def test_a_git_status_that_fails_in_the_copy_fails_as_not_clean(self) -> None:
        runner = PublishingRunner(builds(), failing="status")

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
        runner = PublishingRunner(builds())

        self.run_step(runner)

        clone = runner.commands[2]
        self.assertEqual(clone[:2], ["git", "clone"])
        self.assertEqual(clone[-2], str(self.root))
        self.assertEqual(runner.commands[3][-4:], ["checkout", "--quiet", "--detach", COMMIT])

    def test_each_copy_keeps_the_remote_of_the_working_tree_for_source_link(self) -> None:
        runner = PublishingRunner(builds())

        self.run_step(runner)

        self.assertEqual(runner.commands[4][-3:], ["set-url", "origin", ORIGIN])

    def test_a_repository_without_origin_still_builds(self) -> None:
        runner = PublishingRunner(builds(), origin=FAILURE)

        result = self.run_step(runner)

        self.assertTrue(result.ok, result.details)
        self.assertEqual(runner.containing("set-url"), [])

    def test_both_copies_restore_locked_and_publish_as_continuous_integration(self) -> None:
        runner = PublishingRunner(builds())

        self.run_step(runner)

        restores, publishes = runner.containing("restore"), runner.containing("publish")
        self.assertEqual((len(restores), len(publishes)), (5, 4))
        self.assertIn("--locked-mode", restores[3])
        self.assertIn("-p:ContinuousIntegrationBuild=true", publishes[3])
        self.assertIn("--no-restore", publishes[3])
        self.assertIn("-nodeReuse:false", publishes[3])

    def test_each_target_is_restored_and_published_for_itself(self) -> None:
        runner = PublishingRunner(builds())

        self.run_step(runner)

        restores, publishes = runner.containing("restore"), runner.containing("publish")
        self.assertEqual(restores[1][-1], "-p:RuntimeIdentifier=win-x64")
        self.assertEqual(publishes[1][publishes[1].index("--runtime") + 1], "win-x64")
        self.assertEqual(Path(publishes[1][-1]).name, "win-x64")

    def test_the_two_copies_live_in_different_folders(self) -> None:
        runner = PublishingRunner(builds())

        self.run_step(runner)

        first, second = runner.containing("clone")
        self.assertNotEqual(first[-1], second[-1])

    def test_the_copies_are_deleted_after_the_step(self) -> None:
        runner = PublishingRunner(builds())

        self.run_step(runner)

        output = runner.containing("publish")[0][-1]
        self.assertFalse(Path(output).parent.parent.exists())


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
        self.assertEqual(runner.seen, [{"README.md": "base\n"}] * 4)


if __name__ == "__main__":
    unittest.main()
