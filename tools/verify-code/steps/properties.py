"""Both languages must have executed at least one property, and each property file cites its source.

A side with no property arrangement fails instead of passing empty (issue 41 CA-5). A property that
describes the code it checks, instead of citing the document that wrote the invariant, fails too
(CA-3): the check is that every property file names a requirement, decision or issue by number.
"""

from __future__ import annotations

import re
from pathlib import Path

from steps.common import Context, StepResult
from steps.reports import PASSED, TestOutcome, read_junit, read_trx

NAME = "properties"
GROUP = "report"
DESCRIPTION = "exige propriedades executadas nas duas linguagens, cada uma citando seu documento"

CORE_PROPERTIES_ASSEMBLY = "Core.Tests.Properties.dll"
CORE_PROPERTIES_DIRECTORY = "core/tests/Core.Tests.Properties"
SERVICE_PROPERTIES_MODULE = "properties"
SERVICE_PROPERTIES_DIRECTORY = "language-service/tests/properties"

_CITATION = re.compile(r"(?i)\b(requirement|decision|issue|requisito|decis[ãa]o)\s+\d+")


def core_properties(out_dir: Path) -> list[TestOutcome]:
    """The property tests the core run executed, from its TRX files."""
    found: list[TestOutcome] = []
    for trx in sorted((out_dir / "core").rglob("*.trx")):
        found.extend(o for o in read_trx(trx) if o.source == CORE_PROPERTIES_ASSEMBLY)
    return found


def service_properties(out_dir: Path) -> list[TestOutcome]:
    """The property tests the service run executed, from its JUnit file."""
    junit = out_dir / "service" / "junit.xml"
    if not junit.is_file():
        return []
    return [o for o in read_junit(junit) if SERVICE_PROPERTIES_MODULE in o.source.split(".")]


def uncited(root: Path, directory: str, suffix: str) -> list[str]:
    """Property files that do not cite a requirement, decision or issue by number."""
    base = root / directory
    files = [p for p in sorted(base.rglob(f"*{suffix}")) if "obj" not in p.parts and "bin" not in p.parts]
    return [p.relative_to(root).as_posix() for p in files if not _CITATION.search(p.read_text(encoding="utf-8"))]


def run(ctx: Context) -> StepResult:
    result = StepResult()
    for label, outcomes, directory, suffix in (
        ("núcleo", core_properties(ctx.out_dir), CORE_PROPERTIES_DIRECTORY, ".cs"),
        ("serviço", service_properties(ctx.out_dir), SERVICE_PROPERTIES_DIRECTORY, ".py"),
    ):
        passed = [o for o in outcomes if o.outcome == PASSED]
        if not passed:
            result.fail(f"nenhuma propriedade executada no {label}: o arranjo de propriedades não passa vazio")
        else:
            result.note(f"{label}: {len(passed)} testes de propriedade executados")
        for path in uncited(ctx.root, directory, suffix):
            result.fail(f"{path}: a propriedade não cita o documento de onde vem o invariante")
    return result
