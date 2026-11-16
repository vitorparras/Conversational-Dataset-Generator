"""Tests for the migration_order step against a synthetic git repository."""

from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from steps import migration_order

from . import support

SQL = "core/src/Core.Persistence/Migrations/Sql"


class MigrationOrderTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.repo = support.git_repository(Path(self._tmp.name))

    def publish_on_main(self, *names: str) -> None:
        """Commits the migrations on `main` after the branch forked, as a merged pull request."""
        support.git(self.repo, "checkout", "-q", "main")
        support.commit_files(self.repo, [f"{SQL}/{name}" for name in names], 1)
        support.git(self.repo, "checkout", "-q", "change")

    def add_on_branch(self, *names: str) -> None:
        support.commit_files(self.repo, [f"{SQL}/{name}" for name in names], 1)

    def step(self) -> migration_order.StepResult:
        return migration_order.run(support.context(self.repo, base="main"))

    def test_a_migration_dated_before_the_last_one_of_the_base_fails(self) -> None:
        self.publish_on_main("202611171000-a.sql")
        self.add_on_branch("202611161500-b.sql")

        result = self.step()

        self.assertFalse(result.ok)
        self.assertIn("202611161500-b.sql: ordena antes de 202611171000-a.sql", result.details[0])

    def test_a_migration_dated_after_every_one_of_the_base_passes(self) -> None:
        self.publish_on_main("0054-old.sql", "202611171000-a.sql")
        self.add_on_branch("202611181200-b.sql")

        result = self.step()

        self.assertTrue(result.ok, result.details)
        self.assertIn("1 migrações novas, todas depois das 2 da base", result.details[0])

    def test_a_new_numbered_migration_fails_because_the_name_is_the_moment(self) -> None:
        self.add_on_branch("0055-new.sql")

        result = self.step()

        self.assertFalse(result.ok)
        self.assertIn("leva o nome AAAAMMDDHHMM-nome.sql", result.details[0])

    def test_a_branch_without_new_migrations_passes(self) -> None:
        support.commit_files(self.repo, ["src/a.cs"], 1)

        result = self.step()

        self.assertTrue(result.ok, result.details)

    def test_without_a_base_the_step_fails(self) -> None:
        result = migration_order.run(support.context(self.repo))

        self.assertFalse(result.ok)
        self.assertIn("--base", result.details[0])
