"""Reads the one document that owns the coverage areas, the floors and the critical folders.

The document is docs/operacao/014-modulos-criticos-e-pisos.md. Nothing else in the repository holds
a floor: the steps read the numbers here, so there is no second copy to drift (issue 36 CA-2).

Two tables are read, each by its exact header line:

    | Área | Projeto/pacote | Piso | Motivo |
    | Pasta crítica | Motivo |

A cell in the second column of either table may be written with or without backticks. Backticks mean
the thing exists in the repository (and the documentation check confirms it); plain text means it is
planned and does not exist yet.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path

AREA_HEADER = ("Área", "Projeto/pacote", "Piso", "Motivo")
FOLDER_HEADER = ("Pasta crítica", "Motivo")

PYTHON_PACKAGE = "language_service"
PYTHON_SOURCE = "language-service/src"
DOTNET_SOURCE = "core/src"

#: Top-level folders of the repository, used to turn a machine path into a repository path.
REPOSITORY_FOLDERS = ("core/", "language-service/", "tools/")


@dataclass(frozen=True)
class AreaRow:
    """One line of the areas table."""

    area: str
    project: str
    floor_text: str
    reason: str
    line: int

    @property
    def kind(self) -> str:
        """``path`` (ends with a slash), ``python`` (a module of the service) or ``dotnet`` (a namespace)."""
        if self.project.endswith("/"):
            return "path"
        if self.project.split(".")[0] == PYTHON_PACKAGE:
            return "python"
        return "dotnet"

    @property
    def source_path(self) -> str:
        """The repository path where this area lives (for a Python module, without the extension)."""
        return source_path(self.project)

    def exists(self, root: Path) -> bool:
        """Whether the area is in the repository yet."""
        base = root / self.source_path
        return base.is_dir() or base.with_name(base.name + ".py").is_file()


@dataclass(frozen=True)
class FolderRow:
    """One line of the critical folders table: a folder whose every module needs a row."""

    folder: str
    reason: str
    line: int

    @property
    def path(self) -> str:
        """The repository path of the folder."""
        return source_path(self.folder)


@dataclass
class Document:
    """What was read from the document, and what is wrong with how it is written."""

    areas: list[AreaRow] = field(default_factory=list)
    folders: list[FolderRow] = field(default_factory=list)
    problems: list[str] = field(default_factory=list)


def source_path(project: str) -> str:
    """Where a ``Projeto/pacote`` value lives in the repository."""
    if project.endswith("/"):
        return project.rstrip("/")
    parts = project.split(".")
    if parts[0] == PYTHON_PACKAGE:
        return f"{PYTHON_SOURCE}/" + "/".join(parts)
    assembly = ".".join(parts[:2])
    return "/".join([DOTNET_SOURCE, assembly, *parts[2:]])


def repository_path(raw: str, root: Path) -> str | None:
    """A path a tool wrote (absolute, with either slash) as a path inside the repository, or None."""
    text = raw.replace("\\", "/")
    base = root.resolve().as_posix().rstrip("/") + "/"
    if text.lower().startswith(base.lower()):
        return text[len(base):]
    for marker in REPOSITORY_FOLDERS:
        position = text.find("/" + marker)
        if position >= 0:
            return text[position + 1:]
    return text if not text.startswith("/") and ":" not in text[:3] else None


def _cells(line: str) -> list[str]:
    inner = line.strip()
    if inner.startswith("|"):
        inner = inner[1:]
    if inner.endswith("|"):
        inner = inner[:-1]
    return [cell.strip() for cell in inner.split("|")]


def _plain(cell: str) -> str:
    return cell.strip().strip("`").strip()


def _table_after(lines: list[str], header: tuple[str, ...]) -> tuple[int, list[tuple[int, list[str]]]] | None:
    """The numbered rows under the exact header line, or None when the header is not there."""
    wanted = [h for h in header]
    for index, line in enumerate(lines):
        if line.strip().startswith("|") and _cells(line) == wanted:
            rows: list[tuple[int, list[str]]] = []
            cursor = index + 1
            if cursor < len(lines) and set(lines[cursor].replace("|", "").strip()) <= set("-: "):
                cursor += 1
            while cursor < len(lines) and lines[cursor].strip().startswith("|"):
                rows.append((cursor + 1, _cells(lines[cursor])))
                cursor += 1
            return index + 1, rows
    return None


def read_document(path: Path) -> Document:
    """Parse the document. Every problem found is listed; none is fixed silently."""
    document = Document()
    if not path.is_file():
        document.problems.append(f"{path.name} não existe")
        return document
    lines = path.read_text(encoding="utf-8").splitlines()

    areas = _table_after(lines, AREA_HEADER)
    if areas is None:
        document.problems.append("falta a tabela com o cabeçalho exato '| " + " | ".join(AREA_HEADER) + " |'")
    else:
        seen: dict[str, int] = {}
        for number, cells in areas[1]:
            if len(cells) != len(AREA_HEADER):
                document.problems.append(f"linha {number}: {len(cells)} colunas, esperadas {len(AREA_HEADER)}")
                continue
            area, project, floor, reason = _plain(cells[0]), _plain(cells[1]), _plain(cells[2]), cells[3]
            if not area or not project:
                document.problems.append(f"linha {number}: área ou projeto/pacote vazio")
                continue
            if area in seen:
                document.problems.append(f"linha {number}: área '{area}' repetida (já na linha {seen[area]})")
                continue
            seen[area] = number
            document.areas.append(AreaRow(area, project, floor, reason, number))
        if not document.areas:
            document.problems.append("a tabela de áreas não tem nenhuma linha")

    folders = _table_after(lines, FOLDER_HEADER)
    if folders is None:
        document.problems.append("falta a tabela com o cabeçalho exato '| " + " | ".join(FOLDER_HEADER) + " |'")
    else:
        for number, cells in folders[1]:
            if len(cells) != len(FOLDER_HEADER) or not _plain(cells[0]):
                document.problems.append(f"linha {number}: pasta crítica malformada")
                continue
            document.folders.append(FolderRow(_plain(cells[0]), cells[1], number))
    return document
