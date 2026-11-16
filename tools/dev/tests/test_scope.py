"""The projects a diff reaches: the touched ones and everyone that depends on them."""

from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

import scope
from steps import reach

from . import support

GRAPH: dict[str, set[str]] = {
    "Core.Domain": set(),
    "Core.Platform": {"Core.Domain"},
    "Core.Host": {"Core.Platform"},
    "Core.Domain.Tests": {"Core.Domain"},
    "Core.Host.Tests": {"Core.Host"},
    "Core.Tests.Database": {"Core.Platform"},
    "Core.Platform.Tests": {"Core.Tests.Database", "Core.Platform"},
    "Core.Tests.Architecture": {"Core.Host"},
}


def project(*references: str) -> str:
    items = "".join(f'<ProjectReference Include="../{r}/{r}.csproj" />' for r in references)
    return f"<Project><ItemGroup>{items}</ItemGroup></Project>"


class OwnerTests(unittest.TestCase):
    def test_a_file_of_a_source_project_belongs_to_it(self) -> None:
        self.assertEqual("Core.Host", reach.project_of("core/src/Core.Host/A.cs", GRAPH))

    def test_a_file_of_a_test_project_belongs_to_it(self) -> None:
        owner = reach.project_of("core/tests/Core.Host.Tests/A.cs", GRAPH)

        self.assertEqual("Core.Host.Tests", owner)

    def test_a_file_outside_the_projects_has_no_owner(self) -> None:
        self.assertIsNone(reach.project_of("docs/a.md", GRAPH))
        self.assertIsNone(reach.project_of("core/Core.slnx", GRAPH))

    def test_shared_settings_and_unplaced_core_files_reach_everything(self) -> None:
        for path in ("core/Directory.Build.props", "global.json", ".editorconfig", "core/x.txt"):
            self.assertTrue(scope.reaches_all(path, GRAPH), path)

    def test_a_document_or_a_tool_reaches_no_project(self) -> None:
        for path in ("docs/a.md", "tools/dev/check.py", "core/src/Core.Host/A.cs"):
            self.assertFalse(scope.reaches_all(path, GRAPH), path)


class ReachTests(unittest.TestCase):
    def test_the_dependents_of_a_touched_project_are_reached(self) -> None:
        reached = scope.depending_on(GRAPH, {"Core.Platform"})

        self.assertEqual(
            {
                "Core.Platform",
                "Core.Host",
                "Core.Host.Tests",
                "Core.Tests.Database",
                "Core.Platform.Tests",
                "Core.Tests.Architecture",
            },
            reached,
        )

    def test_building_the_leaves_builds_every_reached_project(self) -> None:
        reached = scope.depending_on(GRAPH, {"Core.Domain"})

        self.assertEqual(
            (
                "Core.Domain.Tests",
                "Core.Host.Tests",
                "Core.Platform.Tests",
                "Core.Tests.Architecture",
            ),
            scope.leaves_of(GRAPH, reached),
        )


class ComputeTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.repo = support.repository(Path(self._tmp.name))
        files = {
            "core/src/Core.Domain/Core.Domain.csproj": project(),
            "core/src/Core.Host/Core.Host.csproj": project("Core.Domain"),
            "core/tests/Core.Domain.Tests/Core.Domain.Tests.csproj": project("Core.Domain"),
            "core/tests/Core.Host.Tests/Core.Host.Tests.csproj": project("Core.Host"),
            "core/tests/Core.Tests.Database/Core.Tests.Database.csproj": project("Core.Host"),
            "core/tests/Core.Db.Tests/Core.Db.Tests.csproj": project("Core.Tests.Database"),
        }
        support.commit(self.repo, files, "chore: projetos")
        support.git(self.repo, "branch", "-q", "base")
        support.git(self.repo, "checkout", "-q", "-b", "feature")

    def test_a_change_in_the_host_runs_only_the_tests_that_need_no_database(self) -> None:
        support.commit(self.repo, {"core/src/Core.Host/A.cs": "class A {}"}, "feat: a")

        found = scope.compute(support.context(self.repo, "base"))

        self.assertFalse(found.everything)
        self.assertEqual(("Core.Host.Tests",), found.tests)
        self.assertEqual(("core/src/Core.Host/A.cs",), found.files)
        self.assertTrue(any("Core.Db.Tests" in line and "banco" in line for line in found.not_run))

    def test_a_change_in_the_domain_reaches_what_depends_on_it(self) -> None:
        support.commit(self.repo, {"core/src/Core.Domain/A.cs": "class A {}"}, "feat: a")

        found = scope.compute(support.context(self.repo, "base"))

        self.assertIn("Core.Host", found.reached)
        self.assertEqual(("Core.Domain.Tests", "Core.Host.Tests"), found.tests)

    def test_shared_settings_reach_every_project(self) -> None:
        support.commit(self.repo, {"core/Directory.Build.props": "<Project/>"}, "build: props")

        found = scope.compute(support.context(self.repo, "base"))

        self.assertTrue(found.everything)
        self.assertEqual(6, len(found.reached))

    def test_a_document_reaches_no_project(self) -> None:
        support.commit(self.repo, {"docs/a.md": "# a"}, "docs: a")

        found = scope.compute(support.context(self.repo, "base"))

        self.assertEqual((), found.reached)
        self.assertEqual((), found.leaves)

    def test_a_diff_that_cannot_be_read_reaches_the_whole_core(self) -> None:
        found = scope.compute(support.context(self.repo, "nao-existe"))

        self.assertTrue(found.everything)
        self.assertEqual(6, len(found.reached))

    def test_an_empty_diff_reaches_nothing(self) -> None:
        found = scope.compute(support.context(self.repo, "feature"))

        self.assertFalse(found.everything)
        self.assertEqual((), found.reached)
