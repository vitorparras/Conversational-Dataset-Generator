"""Tests for what the folder build does without the network: arguments, refusals, bytecode."""

from __future__ import annotations

import contextlib
import io
import marshal
import re
import sys
import tempfile
import unittest
import zipfile
from collections.abc import Sequence
from pathlib import Path
from unittest import mock

import service_folder
import systems

CHECKED_HASH = 0b11
PYC_HEADER_BYTES = 16


def pinned_version() -> str:
    """The version of the backend that language-service/build-requirements.txt pins."""
    text = (service_folder.ROOT / "language-service" / "build-requirements.txt").read_text("utf-8")
    found = re.search(r"^uv-build==(\S+)", text, re.MULTILINE)
    assert found is not None
    return found.group(1)


PINNED = pinned_version()


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

    def record(self, wheel: bool = False, made_by: str = PINNED) -> list[list[str]]:
        """Plays ``run``: records each command and, for ``uv build``, leaves a wheel behind."""
        commands: list[list[str]] = []

        def play(command: Sequence[str | Path], _environment: object = None) -> None:
            words = [str(word) for word in command]
            commands.append(words)
            if wheel and "build" in words:
                out = Path(words[words.index("--out-dir") + 1])
                out.mkdir(parents=True)
                with zipfile.ZipFile(
                    out / "language_service-0.1.0-py3-none-any.whl", "w"
                ) as zipped:
                    zipped.writestr(
                        "language_service-0.1.0.dist-info/WHEEL", f"Generator: uv {made_by}\n"
                    )

        patched = mock.patch.object(service_folder, "run", side_effect=play)
        patched.start()
        self.addCleanup(patched.stop)
        return commands

    def test_a_dependency_without_a_hash_stops_the_install_for_hashes_are_required(self) -> None:
        commands = self.record(wheel=True)

        service_folder.install_packages(self.job(), Path("python"), self.output)

        export, locked = commands[0], commands[1]
        self.assertIn("--frozen", export)
        self.assertIn("--require-hashes", locked)
        self.assertIn("--no-deps", locked)

    def test_the_build_backend_is_installed_by_hash_and_the_build_does_not_resolve_one(
        self,
    ) -> None:
        commands = self.record(wheel=True)

        wheel = service_folder.build_wheel(Path("python"), self.output)

        backend, build = commands[1], commands[2]
        self.assertIn("--require-hashes", backend)
        self.assertEqual(
            str(service_folder.ROOT / service_folder.SERVICE / service_folder.BUILD_REQUIREMENTS),
            backend[backend.index("-r") + 1],
        )
        self.assertIn("--no-build-isolation", build)
        self.assertIn("--force-pep517", build)
        self.assertEqual("language_service-0.1.0-py3-none-any.whl", wheel.name)

    def test_the_service_is_installed_from_the_wheel_and_not_from_its_source(self) -> None:
        commands = self.record(wheel=True)

        service_folder.install_packages(self.job(), Path("python"), self.output)

        own = commands[-1]
        self.assertTrue(own[-1].endswith(".whl"))
        self.assertNotIn("--require-hashes", own)

    def test_the_pinned_backend_has_a_hash_and_is_inside_the_range_of_the_project_file(
        self,
    ) -> None:
        folder = service_folder.ROOT / service_folder.SERVICE
        pinned = (folder / service_folder.BUILD_REQUIREMENTS).read_text(encoding="utf-8")
        project = (folder / "pyproject.toml").read_text(encoding="utf-8")

        version = re.search(r"^uv-build==(\d+)\.(\d+)\.\d+ ", pinned, re.MULTILINE)
        bounds = re.search(r'"uv_build>=(\d+)\.(\d+),<(\d+)\.(\d+)"', project)

        self.assertIsNotNone(version)
        self.assertIsNotNone(bounds)
        assert version is not None and bounds is not None
        low, high = tuple(map(int, bounds.groups()[:2])), tuple(map(int, bounds.groups()[2:]))
        self.assertLessEqual(low, tuple(map(int, version.groups())))
        self.assertLess(tuple(map(int, version.groups())), high)
        self.assertRegex(pinned, r"--hash=sha256:[0-9a-f]{64}")

    def test_build_fails_when_the_build_leaves_no_single_wheel(self) -> None:
        self.record(wheel=False)

        with self.assertRaises(service_folder.BuildError):
            service_folder.build_wheel(Path("python"), self.output)

    def test_a_wheel_built_by_the_embedded_backend_and_not_the_pinned_one_is_refused(self) -> None:
        self.record(wheel=True, made_by="0.11.3")

        with self.assertRaises(service_folder.BuildError):
            service_folder.build_wheel(Path("python"), self.output)

    def test_bytecode_names_its_source_from_the_folder_and_not_from_where_it_was_built(
        self,
    ) -> None:
        python = self.output / "language-service" / "python"
        python.mkdir(parents=True)
        (python / "mod.py").write_text("VALUE = 1\n", encoding="utf-8")

        service_folder.compile_bytecode(Path(sys.executable), python)

        compiled = next((python / "__pycache__").glob("mod.*.pyc")).read_bytes()
        code = marshal.loads(compiled[PYC_HEADER_BYTES:])
        self.assertEqual(str(Path("python") / "mod.py"), code.co_filename)
