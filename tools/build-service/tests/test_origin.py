"""Tests for the origin file against a real git repository in a temporary folder."""

from __future__ import annotations

import json
import subprocess
import tempfile
import unittest
from pathlib import Path

import origin
import sums

LOCK = """version = 1

[[package]]
name = "language-service"
version = "0.1.0"

[[package]]
name = "pt-core-news-md"
version = "3.8.0"
"""
PROJECT = '[project]\nname = "language-service"\nversion = "0.2.0"\n'
GIT_IDENTITY = ["-c", "user.name=t", "-c", "user.email=t@t", "-c", "commit.gpgsign=false"]


def git(root: Path, *arguments: str) -> str:
    done = subprocess.run(
        ["git", *GIT_IDENTITY, "-C", str(root), *arguments],
        capture_output=True,
        text=True,
        check=True,
    )
    return done.stdout.strip()


class OriginTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)
        self.service = self.root / "language-service"
        self.service.mkdir()
        (self.service / origin.LOCK).write_text(LOCK, encoding="utf-8")
        (self.service / origin.PROJECT).write_text(PROJECT, encoding="utf-8")
        git(self.root, "init", "--quiet")
        git(self.root, "add", ".")
        git(self.root, "commit", "--quiet", "-m", "first")

    def read(self) -> origin.Origin:
        return origin.read(self.root, self.service, self.build("pt_core_news_md"))

    def build(self, model: str) -> origin.Build:
        return origin.Build("linux-x64", "3.12.13", model)

    def test_the_commit_is_the_head_of_the_tree_it_was_read_from(self) -> None:
        self.assertEqual(git(self.root, "rev-parse", "HEAD"), self.read().commit)

    def test_a_tree_with_nothing_changed_is_clean(self) -> None:
        self.assertEqual(origin.CLEAN, self.read().tree)

    def test_a_tracked_file_changed_and_not_committed_makes_the_tree_dirty(self) -> None:
        (self.service / origin.PROJECT).write_text(PROJECT + "# edited\n", encoding="utf-8")

        self.assertEqual(origin.DIRTY, self.read().tree)

    def test_a_file_never_added_makes_the_tree_dirty(self) -> None:
        (self.root / "new.py").write_text("X = 1\n", encoding="utf-8")

        self.assertEqual(origin.DIRTY, self.read().tree)

    def test_a_dirty_tree_is_declared_and_not_refused(self) -> None:
        (self.root / "new.py").write_text("X = 1\n", encoding="utf-8")

        text = origin.render(self.read()).decode("utf-8")

        self.assertEqual("dirty", json.loads(text)["tree"])

    def test_the_two_states_are_told_apart_in_the_bytes_of_the_file(self) -> None:
        clean = origin.render(self.read())
        (self.root / "new.py").write_text("X = 1\n", encoding="utf-8")

        self.assertNotEqual(clean, origin.render(self.read()))

    def test_the_versions_come_from_the_project_file_the_lock_and_the_arguments(self) -> None:
        declared = self.read()

        self.assertEqual(
            ("0.2.0", "3.12.13", "pt-core-news-md", "3.8.0", "linux-x64"),
            (
                declared.service,
                declared.python,
                declared.model,
                declared.model_version,
                declared.system,
            ),
        )

    def test_a_model_the_lock_does_not_hold_is_an_error(self) -> None:
        with self.assertRaises(origin.OriginError):
            origin.read(self.root, self.service, self.build("en_core_news_md"))

    def test_a_folder_that_is_not_a_repository_has_no_origin(self) -> None:
        with tempfile.TemporaryDirectory() as bare, self.assertRaises(origin.OriginError):
            origin.tree_state(Path(bare))

    def test_the_file_has_sorted_keys_and_ends_with_one_newline(self) -> None:
        text = origin.render(self.read()).decode("utf-8")

        self.assertTrue(text.endswith("}\n"))
        self.assertEqual(sorted(json.loads(text)), list(json.loads(text)))

    def test_the_manifest_covers_the_file(self) -> None:
        folder = self.root / "folder"
        folder.mkdir()
        origin.write(folder, self.read())

        sums.write(folder)

        self.assertIn(origin.FILE, sums.read(folder))
