"""Tests for the SHA256SUMS manifest against real files in a temporary folder."""

from __future__ import annotations

import os
import tempfile
import unittest
from pathlib import Path

import sums

ABC_DIGEST = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"


class SumsTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.folder = Path(self._tmp.name)
        (self.folder / "python" / "bin").mkdir(parents=True)
        (self.folder / "python" / "bin" / "python3").write_bytes(b"abc")
        (self.folder / "model.bin").write_bytes(b"model")

    def test_manifest_lists_each_file_but_itself_in_the_format_of_sha256sum(self) -> None:
        sums.write(self.folder)

        lines = (self.folder / sums.MANIFEST).read_text(encoding="utf-8").splitlines()

        self.assertEqual(2, len(lines))
        self.assertEqual(f"{ABC_DIGEST}  python/bin/python3", lines[1])
        self.assertTrue(lines[0].endswith("  model.bin"))

    def test_writing_twice_gives_the_same_manifest(self) -> None:
        sums.write(self.folder)
        first = (self.folder / sums.MANIFEST).read_bytes()

        sums.write(self.folder)

        self.assertEqual(first, (self.folder / sums.MANIFEST).read_bytes())

    def test_untouched_folder_has_no_problem(self) -> None:
        sums.write(self.folder)

        self.assertEqual([], sums.problems(self.folder))

    def test_changed_file_is_named(self) -> None:
        sums.write(self.folder)
        (self.folder / "model.bin").write_bytes(b"other")

        self.assertEqual(["alterado: model.bin"], sums.problems(self.folder))

    def test_missing_file_is_named(self) -> None:
        sums.write(self.folder)
        (self.folder / "model.bin").unlink()

        self.assertEqual(["ausente: model.bin"], sums.problems(self.folder))

    def test_new_file_is_named_even_in_a_new_folder(self) -> None:
        sums.write(self.folder)
        (self.folder / "python" / "__pycache__").mkdir()
        (self.folder / "python" / "__pycache__" / "a.pyc").write_bytes(b"x")

        self.assertEqual(["sobrando: python/__pycache__/a.pyc"], sums.problems(self.folder))

    def test_line_out_of_the_format_is_refused(self) -> None:
        (self.folder / sums.MANIFEST).write_text("not a digest  model.bin\n", encoding="utf-8")

        with self.assertRaises(ValueError):
            sums.read(self.folder)

    def test_link_to_a_folder_is_refused(self) -> None:
        try:
            os.symlink(self.folder / "python", self.folder / "link", target_is_directory=True)
        except OSError:
            self.skipTest("this system does not allow creating links")

        with self.assertRaises(ValueError):
            sums.files(self.folder)
