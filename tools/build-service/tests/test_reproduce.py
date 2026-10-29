"""Tests for the proof that the folder is rebuilt equal: the comparison, the offline world, the
clean copy and the verdict, with the builds played and the git repository real."""

from __future__ import annotations

import contextlib
import io
import os
import sys
import tempfile
import unittest
from collections.abc import Mapping
from pathlib import Path
from unittest import mock

import origin
import reproduce
import service_folder
import sums
import systems

from .test_origin import git


def folder(root: Path, files: Mapping[str, bytes]) -> Path:
    """A service folder with these files and the manifest written over them."""
    for name, content in files.items():
        (root / name).parent.mkdir(parents=True, exist_ok=True)
        (root / name).write_bytes(content)
    sums.write(root)
    return root


class ComparisonTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.base = Path(self._tmp.name)

    def listing(self, name: str, files: Mapping[str, bytes]) -> dict[str, str]:
        return reproduce.listing(folder(self.base / name, files))

    def test_folders_with_the_same_bytes_have_nothing_that_differs(self) -> None:
        files = {"python/bin/python3": b"a", "ORIGIN.json": b"{}"}

        result = reproduce.compare(self.listing("one", files), self.listing("two", files))

        self.assertEqual((3, ()), (result.equal, result.unjustified))

    def test_one_byte_in_a_file_names_that_file(self) -> None:
        first = self.listing("one", {"a.py": b"x = 1", "b.py": b"y"})
        second = self.listing("two", {"a.py": b"x = 2", "b.py": b"y"})

        result = reproduce.compare(first, second)

        self.assertEqual(("SHA256SUMS", "a.py"), result.different)

    def test_a_file_in_one_folder_only_is_named_on_its_side(self) -> None:
        first = self.listing("one", {"a.py": b"x", "only_first.py": b"y"})
        second = self.listing("two", {"a.py": b"x", "only_second.py": b"z"})

        result = reproduce.compare(first, second)

        self.assertEqual(("only_first.py",), result.only_first)
        self.assertEqual(("only_second.py",), result.only_second)

    def test_the_manifest_is_compared_too(self) -> None:
        self.assertIn(sums.MANIFEST, self.listing("one", {"a.py": b"x"}))

    def test_a_difference_with_a_reason_does_not_fail_and_one_without_does(self) -> None:
        first = self.listing("one", {"a.py": b"1", "b.py": b"1"})
        second = self.listing("two", {"a.py": b"2", "b.py": b"2"})
        result = reproduce.compare(first, second)

        with mock.patch.object(reproduce, "JUSTIFIED", {"a.py": "r", "SHA256SUMS": "r"}):
            self.assertEqual(("b.py",), result.unjustified)

    def test_the_report_names_each_difference_and_says_when_it_has_no_reason(self) -> None:
        first = self.listing("one", {"a.py": b"1"})
        second = self.listing("two", {"a.py": b"2"})

        lines = reproduce.report(reproduce.compare(first, second))

        self.assertIn("difere: a.py: SEM justificativa", lines)

    @unittest.skipIf(os.name == "nt", "links need a privilege on Windows")
    def test_a_link_is_identified_by_where_it_points(self) -> None:
        (self.base / "a").write_bytes(b"same")
        (self.base / "b").write_bytes(b"same")
        (self.base / "to_a").symlink_to("a")
        (self.base / "to_b").symlink_to("b")

        self.assertNotEqual(
            reproduce.entry(self.base / "to_a"), reproduce.entry(self.base / "to_b")
        )


class OfflineTests(unittest.TestCase):
    def test_uv_is_offline_and_every_proxy_points_at_a_closed_port(self) -> None:
        world = reproduce.offline({"PATH": "p", "HTTPS_PROXY": "http://real:3128", "NO_PROXY": "x"})

        self.assertEqual("1", world["UV_OFFLINE"])
        self.assertEqual(
            {reproduce.CLOSED_PORT_PROXY},
            {world[name] for name in ("HTTPS_PROXY", "https_proxy", "HTTP_PROXY", "http_proxy")},
        )
        self.assertEqual(("", ""), (world["NO_PROXY"], world["no_proxy"]))
        self.assertEqual("p", world["PATH"])


class CopyTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.base = Path(self._tmp.name)
        self.repository = self.base / "repository"
        self.repository.mkdir()
        git(self.repository, "init", "--quiet")
        (self.repository / "a.py").write_text("A = 1\n", encoding="utf-8")
        git(self.repository, "add", ".")
        git(self.repository, "commit", "--quiet", "-m", "first")
        self.commit = git(self.repository, "rev-parse", "HEAD")

    def clone(self) -> Path:
        copy = self.base / "copy"
        with mock.patch.object(reproduce, "ROOT", self.repository):
            reproduce.clone(self.commit, copy, os.environ)
        return copy

    def test_the_copy_is_clean_and_at_the_commit(self) -> None:
        copy = self.clone()

        self.assertEqual(origin.CLEAN, origin.tree_state(copy))
        self.assertEqual(self.commit, git(copy, "rev-parse", "HEAD"))

    def test_a_change_not_committed_in_the_working_tree_does_not_reach_the_copy(self) -> None:
        (self.repository / "a.py").write_text("A = 2\n", encoding="utf-8")

        copy = self.clone()

        self.assertEqual("A = 1\n", (copy / "a.py").read_text(encoding="utf-8"))

    def test_a_commit_that_does_not_exist_is_an_error(self) -> None:
        with (
            mock.patch.object(reproduce, "ROOT", self.repository),
            self.assertRaises(reproduce.ReproductionError),
        ):
            reproduce.clone("0" * 40, self.base / "copy", os.environ)


class VerdictTests(unittest.TestCase):
    """`prove` with the clone and the build played: each build writes the folder it is told to."""

    def prove(self, second: bytes, justified: Mapping[str, str] | None = None) -> tuple[int, str]:
        job = service_folder.Job(systems.LINUX_X64, "pt_core_news_md", Path())
        turns: list[bytes] = [b"same", second]
        worlds: list[Mapping[str, str]] = []

        def build(_copy: Path, built: service_folder.Job, world: Mapping[str, str]) -> float:
            worlds.append(world)
            folder(built.output / service_folder.SERVICE, {"model.bin": turns[len(worlds) - 1]})
            return 1.0

        said = io.StringIO()
        with (
            mock.patch.object(reproduce.origin, "git", return_value="c" * 40),
            mock.patch.object(reproduce, "clone"),
            mock.patch.object(reproduce, "build", side_effect=build),
            mock.patch.object(reproduce, "JUSTIFIED", justified or {}),
            contextlib.redirect_stdout(said),
        ):
            code = reproduce.prove(job, {"PATH": "p"})
        self.worlds = worlds
        return code, said.getvalue()

    def test_folders_equal_byte_by_byte_pass(self) -> None:
        code, output = self.prove(b"same")

        self.assertEqual(0, code)
        self.assertIn("2 arquivos iguais byte a byte", output)

    def test_one_different_byte_fails_and_names_the_file(self) -> None:
        code, output = self.prove(b"other")

        self.assertEqual(1, code)
        self.assertIn("difere: model.bin: SEM justificativa", output)

    def test_a_justified_difference_passes_with_its_reason_printed(self) -> None:
        code, output = self.prove(b"other", {"model.bin": "razão", "SHA256SUMS": "razão"})

        self.assertEqual(0, code)
        self.assertIn("difere: model.bin: razão", output)

    def test_only_the_second_build_has_no_network(self) -> None:
        self.prove(b"same")

        self.assertEqual((False, True), tuple("UV_OFFLINE" in world for world in self.worlds))

    def test_both_builds_share_the_directory_that_keeps_the_interpreter_archive(self) -> None:
        self.prove(b"same")

        self.assertEqual(1, len({world[reproduce.ARCHIVES] for world in self.worlds}))


class ArgumentsTests(unittest.TestCase):
    def test_a_system_that_is_not_this_machine_stops_before_any_clone(self) -> None:
        with (
            mock.patch.object(systems.System, "is_host", return_value=False),
            mock.patch.object(reproduce, "prove") as prove,
            contextlib.redirect_stdout(io.StringIO()),
        ):
            code = reproduce.main(["--system", "win-x64", "--model", "pt_core_news_md"])

        self.assertEqual(1, code)
        prove.assert_not_called()

    def test_a_model_that_is_not_a_package_name_is_refused(self) -> None:
        with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit):
            reproduce.parse(["--system", "linux-x64", "--model", "../x"])

    def test_an_error_of_the_proof_is_said_and_the_exit_code_is_one(self) -> None:
        said = io.StringIO()

        with (
            mock.patch.object(systems.System, "is_host", return_value=True),
            mock.patch.object(reproduce, "prove", side_effect=reproduce.ReproductionError("x")),
            contextlib.redirect_stdout(said),
        ):
            code = reproduce.main(["--system", "linux-x64", "--model", "pt_core_news_md"])

        self.assertEqual(1, code)
        self.assertIn("a prova falhou: x", said.getvalue())


class CommandTests(unittest.TestCase):
    def test_a_command_that_fails_raises_with_the_tail_of_what_it_printed(self) -> None:
        program = "import sys\nprint('boom')\nsys.exit(3)"

        with self.assertRaises(reproduce.ReproductionError) as raised:
            reproduce.command([sys.executable, "-c", program], Path.cwd(), os.environ)

        self.assertIn("boom", str(raised.exception))

    def test_a_command_that_succeeds_returns_without_raising(self) -> None:
        reproduce.command([sys.executable, "-c", "pass"], Path.cwd(), os.environ)
