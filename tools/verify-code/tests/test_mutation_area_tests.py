"""Tests for the test project Stryker runs for an area that has a project of its own."""

from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from steps import mutation_core

from . import support

SOURCE = "Core.Persistence"
REFERENCE = '<ProjectReference Include="..\..\src\Core.Persistence\Core.Persistence.csproj" />'
OTHER = '<ProjectReference Include="..\..\src\Core.Platform\Core.Platform.csproj" />'


class AreaTestProjectTests(unittest.TestCase):
    def setUp(self) -> None:
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        self.root = Path(directory.name)
        for name in ("Migrations", "Content", "ServerGuarantees"):
            self.project(f"Core.Persistence.{name}.Tests", REFERENCE)
        self.project("Core.Persistence.Tests", REFERENCE)

    def project(self, name: str, content: str) -> None:
        support.write(self.root, f"core/tests/{name}/{name}.csproj", content)

    def names(self, area: str | None) -> list[str]:
        found = mutation_core.test_projects(self.root, SOURCE, area)
        return [path.parent.name for path in found]

    def test_an_area_with_a_project_of_its_own_gets_that_project_alone(self) -> None:
        self.assertEqual(
            self.names("Core.Persistence.Migrations"), ["Core.Persistence.Migrations.Tests"]
        )
        self.assertEqual(self.names("Core.Persistence.Content"), ["Core.Persistence.Content.Tests"])
        self.assertEqual(
            self.names("Core.Persistence.ServerGuarantees"),
            ["Core.Persistence.ServerGuarantees.Tests"],
        )

    def test_the_area_that_is_the_project_itself_gets_the_project_of_the_parent_alone(self) -> None:
        self.assertEqual(self.names("Core.Persistence"), ["Core.Persistence.Tests"])

    def test_an_area_without_a_project_of_its_own_gets_the_project_of_what_holds_it(self) -> None:
        self.assertEqual(self.names("Core.Persistence.Connection"), ["Core.Persistence.Tests"])

    def test_without_an_area_the_project_of_the_parent_is_taken(self) -> None:
        self.assertEqual(self.names(None), ["Core.Persistence.Tests"])

    def test_a_project_of_the_area_that_does_not_reference_the_module_is_not_taken(self) -> None:
        self.project("Core.Persistence.Content.Tests", OTHER)

        self.assertEqual(self.names("Core.Persistence.Content"), ["Core.Persistence.Tests"])

    def test_the_project_of_one_area_is_never_taken_for_another(self) -> None:
        self.assertNotIn(
            "Core.Persistence.Migrations.Tests", self.names("Core.Persistence.Content")
        )
