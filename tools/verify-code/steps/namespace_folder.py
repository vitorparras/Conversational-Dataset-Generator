"""Confers that the project/package of docs/operacao/014 holds as namespace and as folder: a `.cs`
file of core/src falls in one area by its namespace and must fall in the same one by its folder.

The service needs no such check: its module is its file path, so the two coincide by construction.
"""

from __future__ import annotations

import re
from pathlib import Path

from steps.areas_doc import DOCUMENT, Area, area_of, read_areas

SOURCES = "core/src"
NAMESPACE = re.compile(r"^\s*namespace\s+([A-Za-z_][\w.]*)", re.MULTILINE)
BUILD_OUTPUT = frozenset({"bin", "obj"})


def folder_unit(relative: Path) -> str:
    """The dotted unit of the folder of a file under core/src, project folder first."""
    return ".".join(relative.parent.parts)


def named(area: Area | None) -> str:
    return "nenhuma área" if area is None else f"{area.name} (`{area.prefix}`)"


def in_file(base: Path, path: Path, areas: tuple[Area, ...]) -> tuple[str, ...]:
    """A failure per namespace of the file whose area is not the area of the file's folder."""
    relative = path.relative_to(base)
    by_folder = area_of(folder_unit(relative), areas)
    text = path.read_text(encoding="utf-8-sig", errors="replace")
    wrong = (ns for ns in dict.fromkeys(NAMESPACE.findall(text)) if area_of(ns, areas) != by_folder)
    return tuple(
        f"{SOURCES}/{relative.as_posix()}: o namespace `{ns}` cai em {named(area_of(ns, areas))} e "
        f"a pasta em {named(by_folder)}; Projeto/pacote de {DOCUMENT} vale como namespace e pasta"
        for ns in wrong
    )


def divergences(root: Path, areas: tuple[Area, ...]) -> tuple[str, ...]:
    """A failure per file and namespace whose area by namespace is not its area by folder."""
    base = root / SOURCES
    files = sorted(base.rglob("*.cs")) if base.is_dir() else []
    sources = (path for path in files if not BUILD_OUTPUT & set(path.relative_to(base).parts))
    return tuple(line for path in sources for line in in_file(base, path, areas))


def check(root: Path) -> tuple[str, ...]:
    """The divergences of the repository; none when the table cannot be read, which other steps
    of the judgement already name."""
    table = read_areas(root)
    return () if table.problems else divergences(root, table.areas)
