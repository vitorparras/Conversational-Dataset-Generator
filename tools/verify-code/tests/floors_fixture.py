"""Shared fixture of the floors tests: a synthetic document and reports in a temporary root."""

from __future__ import annotations

import dataclasses
import os
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from steps import areas_doc, coverage_by_area, floors, test_core, test_service
from steps.common import StepResult

from . import support

DOCUMENT = "\n".join(
    (
        "# 014",
        areas_doc.HEADER,
        "|---|---|---|---|---|",
        "| Registro do núcleo | `Core.Platform.Logging` | 90 % | sim | crítica |",
        "| Plataforma | `Core.Platform` | 80 % | não | demais pastas |",
        "| Contrato | `Core.Boundary.Contracts` | isento | não | só tipos |",
        "| Texto | `Core.Boundary.Text` | 90 % | sim | ainda sem código |",
        "| Autenticação | `language_service.auth` | 90 % | sim | segredo |",
        "| Serviço | `language_service` | 80 % | não | demais módulos |",
        "",
    )
)
WITHOUT_SERVICE_ROOT = DOCUMENT.replace("| `language_service` |", "| `language_service.app` |")
LOGGING = "Core.Platform.Logging.Formatter"
STARTUP = "Core.Platform.Startup.Runner"
CORE_REPORT = f"{test_core.RESULTS}/a.cobertura.xml"
SERVICE_REPORT = f"{test_service.RESULTS}/{test_service.REPORT}"
PREVIOUS = "previous/by-area.md"
LOWER_BEFORE = "\n".join((*coverage_by_area.HEADER, "| Plataforma | 80 % | 75,0 % | 3 de 4 |", ""))
SAME_BEFORE = "\n".join((*coverage_by_area.HEADER, "| Plataforma | 80 % | 80,0 % | 8 de 10 |", ""))
HIGHER_BEFORE = "\n".join(
    (*coverage_by_area.HEADER, "| Plataforma | 80 % | 100,0 % | 3 de 3 |", "")
)


class FloorsFixture(unittest.TestCase):
    """A temporary repository with the synthetic document; no test of its own."""

    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)
        support.write(self.root, areas_doc.DOCUMENT, DOCUMENT)
        self.environment = mock.patch.dict(os.environ, clear=False)
        self.environment.start()
        self.addCleanup(self.environment.stop)
        os.environ.pop(coverage_by_area.SUMMARY, None)

    def core(self, *classes: tuple[str, str, str], package: str = "Core.Platform") -> None:
        report = support.cobertura(package, *classes)
        support.write(self.root / ".verify-code", CORE_REPORT, report)

    def folder(self, relative: str) -> None:
        (self.root / relative).mkdir(parents=True)

    def project(self, name: str) -> None:
        support.write(self.root, f"core/src/{name}/{name}.csproj", "<Project />\n")

    def package(self, name: str) -> None:
        support.write(self.root, f"language-service/src/language_service/{name}/__init__.py", "")

    def run_step(self, previous: Path | None = None) -> StepResult:
        ctx = support.context(self.root, support.FakeRunner())
        return floors.run(dataclasses.replace(ctx, previous=previous))
