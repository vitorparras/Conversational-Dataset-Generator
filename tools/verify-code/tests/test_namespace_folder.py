"""Tests for the namespace and folder check of the floors step: a core file falls in one area."""

from __future__ import annotations

import unittest

from steps import areas_doc, namespace_folder

from . import floors_fixture as fixture
from . import support
from .floors_fixture import LOGGING

REPOSITORY = support.VERIFY_CODE.parents[1]
PLATFORM_FILE = "core/src/Core.Platform/Logging/Formatter.cs"
FILE_SCOPED = "namespace Core.Platform.Logging;\n\npublic sealed class Formatter;\n"


class NamespaceFolderTests(fixture.FloorsFixture):
    def source(self, relative: str, text: str) -> None:
        support.write(self.root, relative, text)

    def test_a_file_whose_namespace_and_folder_share_an_area_passes(self) -> None:
        self.source(PLATFORM_FILE, FILE_SCOPED)

        self.assertEqual(namespace_folder.check(self.root), ())

    def test_a_deeper_namespace_in_the_same_area_passes(self) -> None:
        self.source(PLATFORM_FILE, "namespace Core.Platform.Logging.Internal;\n")

        self.assertEqual(namespace_folder.check(self.root), ())

    def test_a_namespace_in_another_area_than_the_folder_fails_naming_file_and_areas(self) -> None:
        self.source(PLATFORM_FILE, "namespace Core.Platform;\n")

        found = namespace_folder.check(self.root)

        expected = (
            f"core/src/Core.Platform/Logging/Formatter.cs: o namespace `Core.Platform` cai em "
            "Plataforma (`Core.Platform`) e a pasta em Registro do núcleo "
            f"(`Core.Platform.Logging`); Projeto/pacote de {areas_doc.DOCUMENT} vale como "
            "namespace e pasta"
        )
        self.assertEqual(found, (expected,))

    def test_the_block_form_of_a_namespace_is_read(self) -> None:
        self.source(
            "core/src/Core.Platform/Startup/Runner.cs", "namespace Core.Platform.Logging\n{\n}\n"
        )

        found = namespace_folder.check(self.root)

        self.assertEqual(len(found), 1)
        self.assertIn("o namespace `Core.Platform.Logging` cai em Registro do núcleo", found[0])

    def test_a_namespace_no_area_owns_is_named_as_without_area(self) -> None:
        self.source(PLATFORM_FILE, "namespace Other.Thing;\n")

        found = namespace_folder.check(self.root)

        self.assertIn("o namespace `Other.Thing` cai em nenhuma área e a pasta em", found[0])

    def test_every_namespace_of_a_file_is_checked(self) -> None:
        text = "namespace Core.Platform.Logging { }\nnamespace Core.Platform { }\n"
        self.source(PLATFORM_FILE, text)

        found = namespace_folder.check(self.root)

        self.assertEqual(len(found), 1)

    def test_a_file_without_namespace_is_not_a_divergence(self) -> None:
        self.source(PLATFORM_FILE, "global using System;\n")

        self.assertEqual(namespace_folder.check(self.root), ())

    def test_build_output_is_not_checked(self) -> None:
        self.source("core/src/Core.Platform/obj/Debug/Generated.cs", "namespace Other;\n")
        self.source("core/src/Core.Platform/bin/Generated.cs", "namespace Other;\n")

        self.assertEqual(namespace_folder.check(self.root), ())

    def test_a_document_that_does_not_read_checks_nothing(self) -> None:
        support.write(self.root, areas_doc.DOCUMENT, "# 014\n")
        self.source(PLATFORM_FILE, "namespace Core.Platform;\n")

        self.assertEqual(namespace_folder.check(self.root), ())

    def test_the_floors_step_fails_naming_the_file_even_with_reports(self) -> None:
        self.source(PLATFORM_FILE, "namespace Core.Platform;\n")
        self.core((LOGGING, "L.cs", "1"))

        result = self.run_step()

        self.assertFalse(result.ok)
        self.assertTrue(any(line.startswith(PLATFORM_FILE) for line in result.details))

    def test_the_floors_step_fails_naming_the_file_without_any_report(self) -> None:
        self.source(PLATFORM_FILE, "namespace Core.Platform;\n")

        result = self.run_step()

        self.assertFalse(result.ok)
        self.assertTrue(any(line.startswith(PLATFORM_FILE) for line in result.details))

    def test_the_floors_step_passes_when_namespace_and_folder_agree(self) -> None:
        self.source(PLATFORM_FILE, FILE_SCOPED)
        self.core((LOGGING, "L.cs", "1"))

        result = self.run_step()

        self.assertTrue(result.ok)


class RepositoryTests(unittest.TestCase):
    def test_the_core_of_the_repository_has_no_divergence(self) -> None:
        self.assertEqual(namespace_folder.check(REPOSITORY), ())
        self.assertNotEqual(list((REPOSITORY / "core/src").rglob("*.cs")), [])
