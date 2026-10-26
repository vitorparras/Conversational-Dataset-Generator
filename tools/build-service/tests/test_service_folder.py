"""Tests for what the folder build does without the network: arguments, refusals, bytecode."""

from __future__ import annotations

import contextlib
import io
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

import service_folder
import systems

CHECKED_HASH = 0b11


class BuildTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.output = Path(self._tmp.name)

    def request(self) -> service_folder.Request:
        return service_folder.Request(systems.LINUX_X64, "pt_core_news_md", self.output)

    def test_group_of_the_model_is_named_as_uv_normalizes_it(self) -> None:
        self.assertEqual("pt-core-news-md", self.request().group)

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
            service_folder.assemble(self.request())

        self.assertTrue((self.output / service_folder.SERVICE).is_dir())

    def test_bytecode_is_rewritten_as_checked_hash(self) -> None:
        source = self.output / "pkg"
        (source / "__pycache__").mkdir(parents=True)
        (source / "mod.py").write_text("VALUE = 1\n", encoding="utf-8")
        (source / "__pycache__" / "mod.cpython-312.pyc").write_bytes(b"stale")

        service_folder.compile_bytecode(Path(sys.executable), source)

        compiled = next((source / "__pycache__").glob("mod.*.pyc")).read_bytes()
        self.assertEqual(CHECKED_HASH, int.from_bytes(compiled[4:8], "little"))
