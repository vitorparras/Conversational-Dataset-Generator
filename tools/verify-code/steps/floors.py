"""Compares the coverage of each area with the floor the operations document declares.

The floor is read from docs/operacao/014-modulos-criticos-e-pisos.md and from nowhere else. The step
fails closed: an area without a floor, a floor that is not a number, a table that is not where it
should be, and a module in a critical folder that no row lists are all failures, never a pass.
"""

from __future__ import annotations

import json
import re
from pathlib import Path

from steps.areas_doc import Document, FolderRow, read_document
from steps.common import FLOORS_DOCUMENT, Context, StepResult
from steps.coverage_by_area import ABSENT, MEASURED, PREVIOUS_FILE, REPORT_FILE, UNMEASURED

NAME = "floors"
GROUP = "report"
DESCRIPTION = "reprova área abaixo do piso, sem piso ou módulo crítico fora da lista"

_FLOOR = re.compile(r"^(\d{1,3}(?:[.,]\d+)?)\s*%?$")

#: Folders that hold no module of their own.
_SKIPPED_DIRECTORIES = {"bin", "obj", "tests", "__pycache__"}
#: Files that are an entry point or a package marker, not a module a floor would apply to.
_SKIPPED_FILES = {"__init__.py", "__main__.py"}
_CODE_SUFFIXES = {".cs", ".py"}


def parse_floor(text: str) -> float | None:
    """A floor written as ``80``, ``80%`` or ``92,5``; None for anything else, 0 to 100 only."""
    found = _FLOOR.match(text.strip())
    if not found:
        return None
    value = float(found.group(1).replace(",", "."))
    return value if 0 <= value <= 100 else None


def modules_of(folder: FolderRow, root: Path) -> list[str]:
    """The modules of a critical folder: its direct subfolders and code files, as repository paths."""
    base = root / folder.path
    if not base.is_dir():
        return []
    modules: list[str] = []
    for child in sorted(base.iterdir()):
        if child.name.startswith(".") or child.name in _SKIPPED_DIRECTORIES:
            continue
        if child.is_dir():
            modules.append(f"{folder.path}/{child.name}")
        elif child.suffix in _CODE_SUFFIXES and child.name not in _SKIPPED_FILES:
            modules.append(f"{folder.path}/{child.stem}")
    return modules


def check_critical_folders(document: Document, root: Path, result: StepResult) -> None:
    """Every module in a critical folder must have a row (issue 36 CA-3); the module is named."""
    listed = [row.source_path for row in document.areas]
    for folder in document.folders:
        for module in modules_of(folder, root):
            if not any(path == module or path.startswith(module + "/") for path in listed):
                result.fail(
                    f"módulo '{module}' está na pasta crítica '{folder.folder}' e não tem linha na "
                    f"tabela de áreas de {FLOORS_DOCUMENT}"
                )


def read_report(path: Path) -> dict[str, dict[str, object]] | None:
    """The areas of a coverage report written by coverage_by_area, by name."""
    if not path.is_file():
        return None
    data = json.loads(path.read_text(encoding="utf-8"))
    return {entry["area"]: entry for entry in data["areas"]}


def run(ctx: Context) -> StepResult:
    result = StepResult()
    document = read_document(ctx.root / FLOORS_DOCUMENT)
    if document.problems:
        for problem in document.problems:
            result.fail(f"{FLOORS_DOCUMENT}: {problem}")
        return result

    measured = read_report(ctx.out_dir / REPORT_FILE)
    if measured is None:
        result.fail(f"não há {REPORT_FILE} em {ctx.out_dir.name}: rode o passo coverage_by_area antes")
        return result
    previous = read_report(ctx.out_dir / PREVIOUS_FILE) or {}

    table = ["| Área | Piso | Medido | Situação |", "|---|---|---|---|"]
    declared = {row.area for row in document.areas}
    for name in sorted(set(measured) - declared):
        result.fail(f"área '{name}' foi medida e não tem piso na tabela de áreas")

    for row in document.areas:
        floor = parse_floor(row.floor_text)
        found = measured.get(row.area)
        if floor is None:
            result.fail(f"área '{row.area}': piso '{row.floor_text}' não é um número de 0 a 100")
            table.append(f"| {row.area} | {row.floor_text or '(vazio)'} | - | piso inválido |")
            continue
        if found is None:
            result.fail(f"área '{row.area}' tem piso e não foi medida: rode coverage_by_area de novo")
            continue
        percent, status = float(found["percent"]), str(found["status"])  # type: ignore[arg-type]
        if status == ABSENT:
            result.warn(f"área '{row.area}': ainda não existe no código; o piso {floor:g}% vale a partir da etapa que a cria")
            situation = "pendente: a área não existe ainda"
        elif status == UNMEASURED and floor > 0:
            result.fail(f"área '{row.area}': existe no código e nenhuma linha foi medida (piso {floor:g}%)")
            situation = "FALHA: sem linha medida"
        elif status == MEASURED and percent < floor:
            result.fail(f"área '{row.area}': medido {percent:g}% abaixo do piso {floor:g}%")
            situation = "FALHA: abaixo do piso"
        else:
            situation = "ok"
        old = previous.get(row.area)
        if old is not None and percent < float(old["percent"]) and situation == "ok":  # type: ignore[arg-type]
            result.warn(f"área '{row.area}': cobertura caiu de {float(old['percent']):g}% para {percent:g}% sem cruzar o piso {floor:g}%")  # type: ignore[arg-type]
            situation = f"ok, mas caiu de {float(old['percent']):g}%"  # type: ignore[arg-type]
        table.append(f"| {row.area} | {floor:g}% | {percent:.2f}% | {situation} |")

    check_critical_folders(document, ctx.root, result)
    result.lines.extend(table)
    if ctx.summary_path is not None:
        with ctx.summary_path.open("a", encoding="utf-8", newline="\n") as summary:
            summary.write("### Pisos de cobertura\n\n" + "\n".join(table) + "\n\n")
    return result
