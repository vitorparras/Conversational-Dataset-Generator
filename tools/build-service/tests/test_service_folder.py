"""Tests for what the folder build does without the network: arguments, refusals, bytecode."""

from __future__ import annotations

import contextlib
import io
import marshal
import sys
import tempfile
import unittest
from collections.abc import Sequence
from pathlib import Path
from unittest import mock

import service_folder
import systems

CHECKED_HASH = 0b11
PYC_HEADER_BYTES = 16


class BuildTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.output = Path(self._tmp.name)

    def job(self) -> service_folder.Job:
        return service_folder.Job(systems.LINUX_X64, "pt_core_news_md", self.output)

    def test_group_of_the_model_is_named_as_uv_normalizes_it(self) -> None:
        self.assertEqual("pt-core-news-md", self.job().group)

    def test_model_that_is_not_a_package_name_is_refused(self) -> None:
        arguments = ["--system", "linux-x64", "--model", "../x", "--output", str(self.output)]

        with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit):
            service_folder.parse(arguments)

    def test_unknown_system_is_refused(self) -> None:
        arguments = ["--system", "mac", "--model", "pt_core_news_md", "--output", str(self.output)]

        with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit):
            service_folder.parse(arguments)

    def test_system_that_is_not_the_host_stops_before_building(self) -> None:
        arguments = [
            "--system",
            "win-x64",
            "--model",
            "pt_core_news_md",
            "--output",
            str(self.output),
        ]
        said = io.StringIO()

        with (
            mock.patch.object(systems.System, "is_host", return_value=False),
            mock.patch.object(service_folder, "assemble") as assemble,
            contextlib.redirect_stdout(said),
        ):
            code = service_folder.main(arguments)

        self.assertEqual(1, code)
        assemble.assert_not_called()
        self.assertIn("win-x64", said.getvalue())

    def test_existing_folder_is_never_overwritten(self) -> None:
        (self.output / service_folder.SERVICE).mkdir()

        with self.assertRaises(service_folder.BuildError):
            service_folder.assemble(self.job())

        self.assertTrue((self.output / service_folder.SERVICE).is_dir())

    def test_bytecode_is_rewritten_as_checked_hash(self) -> None:
        source = self.output / "pkg"
        (source / "__pycache__").mkdir(parents=True)
        (source / "mod.py").write_text("VALUE = 1\n", encoding="utf-8")
        (source / "__pycache__" / "mod.cpython-312.pyc").write_bytes(b"stale")

        service_folder.compile_bytecode(Path(sys.executable), source)

        compiled = next((source / "__pycache__").glob("mod.*.pyc")).read_bytes()
        self.assertEqual(CHECKED_HASH, int.from_bytes(compiled[4:8], "little"))

    def test_a_dependency_without_a_hash_stops_the_install_for_it_is_required_and_never_resolved(
        self,
    ) -> None:
        commands: list[list[str]] = []

        def record(command: Sequence[str | Path], _environment: object = None) -> None:
            commands.append([str(word) for word in command])

        with mock.patch.object(service_folder, "run", side_effect=record):
            service_folder.install_packages(self.job(), Path("python"), self.output)

        export, locked, own = commands
        self.assertIn("--frozen", export)
        self.assertIn("--require-hashes", locked)
        self.assertIn("--no-deps", locked)
        self.assertNotIn("--require-hashes", own)

    def test_bytecode_names_its_source_from_the_folder_and_not_from_where_it_was_built(
        self,
    ) -> None:
        folder = self.output / "language-service"
        python = folder / "python"
        python.mkdir(parents=True)
        (python / "mod.py").write_text("VALUE = 1\n", encoding="utf-8")

        service_folder.compile_bytecode(Path(sys.executable), python)

        compiled = next((python / "__pycache__").glob("mod.*.pyc")).read_bytes()
        code = marshal.loads(compiled[PYC_HEADER_BYTES:])
        self.assertEqual(str(Path("python") / "mod.py"), code.co_filename)
