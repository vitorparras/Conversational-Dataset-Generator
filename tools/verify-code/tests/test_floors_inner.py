"""Tests for the floors step on the first level inside a project or package: a folder or module
with no row of its own that no `Absorve:` enumeration names fails, in both languages."""

from __future__ import annotations

import shutil

from steps import areas_doc
from steps.common import StepResult

from . import floors_fixture as fixture
from . import support
from .floors_fixture import LOGGING

SERVICE = "language-service/src/language_service"
DOCUMENT = "\n".join(
    (
        "# 014",
        areas_doc.HEADER,
        "|---|---|---|---|---|",
        "| Registro | `Core.Platform.Logging` | 90 % | sim | crítica |",
        "| Plataforma | `Core.Platform` | 80 % | não | Absorve: `Versions`, `Clock`; portas |",
        "| Persistência | `Core.Persistence` | 80 % | não | demais pastas |",
        "| Autenticação | `language_service.auth` | 90 % | sim | segredo |",
        "| Serviço | `language_service` | 80 % | não | Absorve: `app`, `routes`; demais |",
        "",
    )
)


def gaps(result: StepResult) -> list[str]:
    """The failures about a first-level folder or module no row names; the rest needs reports."""
    return [line for line in result.details if "`Absorve:`" in line]


class FloorsInnerTests(fixture.FloorsFixture):
    def setUp(self) -> None:
        super().setUp()
        support.write(self.root, areas_doc.DOCUMENT, DOCUMENT)
        self.core((LOGGING, "L.cs", "1"))
        self.project("Core.Platform")
        for name in ("Versions", "Clock"):
            self.source(f"core/src/Core.Platform/{name}/{name}.cs")
        for name in ("app", "routes"):
            self.source(f"{SERVICE}/{name}.py")

    def source(self, relative: str) -> None:
        support.write(self.root, relative, "namespace X;\n")

    def test_a_new_folder_of_a_project_no_row_names_fails_naming_it(self) -> None:
        self.project("Core.Persistence")
        self.source("core/src/Core.Persistence/Custody/Vault.cs")

        result = self.run_step()

        self.assertFalse(result.ok)
        folder = "core/src/Core.Persistence/Custody/"
        self.assertEqual(
            gaps(result)[0],
            f"{folder}: sem linha em {areas_doc.DOCUMENT} nem enumerada em `Absorve:` na linha "
            "`Core.Persistence`",
        )

    def test_a_folder_the_row_of_its_project_enumerates_passes(self) -> None:
        self.project("Core.Platform")

        self.assertEqual(gaps(self.run_step()), [])

    def test_a_folder_with_a_row_of_its_own_passes(self) -> None:
        self.project("Core.Platform")
        self.source("core/src/Core.Platform/Logging/Formatter.cs")

        self.assertEqual(gaps(self.run_step()), [])

    def test_a_folder_a_row_of_another_project_enumerates_still_fails(self) -> None:
        self.project("Core.Persistence")
        self.source("core/src/Core.Persistence/Versions/Reader.cs")

        result = self.run_step()

        self.assertFalse(result.ok)
        self.assertIn("core/src/Core.Persistence/Versions/", gaps(result)[0])

    def test_build_output_and_folders_without_source_are_not_judged(self) -> None:
        self.project("Core.Persistence")
        self.source("core/src/Core.Persistence/obj/Generated.cs")
        self.source("core/src/Core.Persistence/bin/Generated.cs")
        self.folder("core/src/Core.Persistence/Empty")
        self.source("core/src/Core.Persistence/Root.cs")

        self.assertEqual(gaps(self.run_step()), [])

    def test_a_new_module_of_the_service_fails_naming_it(self) -> None:
        self.source(f"{SERVICE}/custody.py")

        result = self.run_step()

        self.assertFalse(result.ok)
        self.assertEqual(
            gaps(result)[0],
            f"{SERVICE}/custody.py: sem linha em {areas_doc.DOCUMENT} nem enumerada em "
            "`Absorve:` na linha `language_service`",
        )

    def test_a_new_package_of_the_service_fails_naming_it(self) -> None:
        self.package("custody")

        result = self.run_step()

        self.assertFalse(result.ok)
        self.assertIn(f"{SERVICE}/custody/: sem linha", gaps(result)[0])

    def test_modules_the_row_enumerates_or_that_have_a_row_of_their_own_pass(self) -> None:
        self.package("auth")

        self.assertEqual(gaps(self.run_step()), [])

    def test_dunder_files_and_caches_of_the_service_are_not_judged(self) -> None:
        for name in ("__init__", "__main__"):
            self.source(f"{SERVICE}/{name}.py")
        self.source(f"{SERVICE}/__pycache__/app.py")

        self.assertEqual(gaps(self.run_step()), [])

    def test_a_folder_of_a_project_no_row_owns_is_named_once_by_the_orphan_check(self) -> None:
        self.project("Core.Vault")
        self.source("core/src/Core.Vault/Custody/Vault.cs")

        result = self.run_step()

        self.assertFalse(result.ok)
        self.assertEqual([d for d in result.details if "Custody" in d], [])
        self.assertEqual(result.details[0].split(":")[0], "core/src/Core.Vault/")

    def test_a_folder_the_row_enumerates_that_is_gone_fails_naming_it(self) -> None:
        shutil.rmtree(self.root / "core/src/Core.Platform/Clock")

        result = self.run_step()

        self.assertFalse(result.ok)
        self.assertEqual(
            gaps(result),
            [
                f"{areas_doc.DOCUMENT}: `Clock` em `Absorve:` de `Core.Platform` "
                "e não existe mais; tire-o da lista"
            ],
        )

    def test_a_module_the_row_enumerates_that_is_gone_fails_naming_it(self) -> None:
        (self.root / SERVICE / "routes.py").unlink()

        result = self.run_step()

        self.assertFalse(result.ok)
        self.assertIn("`routes` em `Absorve:` de `language_service`", gaps(result)[0])
