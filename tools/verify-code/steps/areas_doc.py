"""Reads the coverage areas from the folder docs/operacao/areas, the only copy of the floors.

Each area is one file named by its project or package, with the title as the area name and four
fixed fields, one per line: `- Projeto/pacote: `A.B``, `- Piso: 90 %` (or `isento`),
`- Crítica: sim` (or `não`) and `- Motivo: ...`, which goes on in lines indented by two spaces.
"""

from __future__ import annotations

import re
from dataclasses import dataclass
from pathlib import Path

FOLDER = "docs/operacao/areas"
OWNER = "Projeto/pacote"
FIELDS = (OWNER, "Piso", "Crítica", "Motivo")
EXEMPT = "isento"
YES = "sim"
NO = "não"
TITLE = re.compile(r"# (.+)")
CONTINUATION = re.compile(r"  \S.*")
FIELD = re.compile(r"- (Projeto/pacote|Piso|Crítica|Motivo): (.*)")
FLOOR = re.compile(r"(\d{1,3}) ?%")
PREFIX = re.compile(r"`([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)`")
ABSORBS = re.compile(r"Absorve: ([^;]*)")
NAME = re.compile(r"`([A-Za-z_]\w*)`")
WHOLE = 100


@dataclass(frozen=True)
class Area:
    """One file of the folder: a name, the prefix it owns, its floor (absent when exempt), whether
    it is a critical module, the list the mutation test runs over, and the first-level folders or
    modules it absorbs, which its reason enumerates after `Absorve:` up to the next `;`."""

    name: str
    prefix: str
    floor: int | None
    critical: bool = False
    absorbs: tuple[str, ...] = ()


@dataclass(frozen=True)
class AreaTable:
    """The areas of the folder, or the problems that keep it from being read."""

    areas: tuple[Area, ...]
    problems: tuple[str, ...]


def within(unit: str, prefix: str) -> bool:
    """Whether ``unit`` is ``prefix`` or sits under it, segment by segment."""
    return unit == prefix or unit.startswith(f"{prefix}.")


def area_of(unit: str, areas: tuple[Area, ...]) -> Area | None:
    """The area with the longest prefix that owns ``unit``, or None when no area does."""
    owners = [area for area in areas if within(unit, area.prefix)]
    return max(owners, key=lambda area: len(area.prefix), default=None)


def fields_of(path: str, lines: list[str]) -> tuple[dict[str, str], list[str]]:
    """The value of each fixed field of one file and the problems of its title and fields."""
    found: dict[str, str] = {}
    problems: list[str] = []
    if not lines or TITLE.fullmatch(lines[0]) is None:
        problems.append(f"{path}:1: falta o título `# Nome da área` na primeira linha")
    current = ""
    for number, line in enumerate(lines, start=1):
        field = FIELD.fullmatch(line)
        if field is None:
            # A line indented by two spaces goes on with the value of the field above it.
            if current and CONTINUATION.fullmatch(line):
                found[current] = f"{found[current]} {line.strip()}"
            else:
                current = ""
            continue
        current = field[1]
        if current in found:
            problems.append(f"{path}:{number}: o campo `{current}` já aparece antes")
        found.setdefault(current, field[2].strip())
    problems.extend(f"{path}: falta o campo `{name}`" for name in FIELDS if name not in found)
    return found, problems


def parse_file(path: str, lines: list[str]) -> tuple[Area | None, list[str]]:
    """The area one file declares, or none, and the problems with it, each naming the file."""
    found, problems = fields_of(path, lines)
    if problems:
        return None, problems
    name = lines[0].removeprefix("# ").strip()
    owner = PREFIX.fullmatch(found[OWNER])
    if owner is None:
        return None, [f"{path}: projeto/pacote '{found[OWNER]}' fora da forma `A.B` com crases"]
    floor, critical, reason = found["Piso"], found["Crítica"], found["Motivo"]
    if critical not in (YES, NO):
        return None, [f"{path}: Crítica '{critical}' não é `{YES}` nem `{NO}`"]
    listed = ABSORBS.search(reason)
    absorbs = tuple(NAME.findall(listed.group(1))) if listed else ()
    number = FLOOR.fullmatch(floor)
    if floor != EXEMPT and (number is None or int(number.group(1)) > WHOLE):
        return None, [
            f"{path}: piso '{floor}' não é porcentagem de 0 a 100 (`90 %`) nem `{EXEMPT}`"
        ]
    value = None if number is None else int(number.group(1))
    return Area(name, owner.group(1), value, critical == YES, absorbs), []


def misnamed(path: str, area: Area) -> list[str]:
    """A problem when the file is not named by the project or package it declares."""
    expected = f"{FOLDER}/{area.prefix}.md"
    return (
        []
        if path == expected
        else [f"{path}: o arquivo se chama {expected.rsplit('/', maxsplit=1)[-1]}"]
    )


def duplicates(declared: list[tuple[str, Area]]) -> list[str]:
    """A problem for each file whose area name or project/package an earlier file declared."""
    first: dict[tuple[str, str], str] = {}
    found: list[str] = []
    for path, area in declared:
        for key in (("name", area.name), ("prefix", area.prefix)):
            if key in first:
                found.append(f"{path}: `{key[1]}` já aparece em {first[key]}")
            first.setdefault(key, path)
    return found


def read_areas(root: Path) -> AreaTable:
    """The areas of the folder under ``root``; any malformed file fails the whole read."""
    folder = root / FOLDER
    if not folder.is_dir():
        return AreaTable((), (f"{FOLDER} não existe: não há áreas para medir",))
    entries = sorted(folder.iterdir())
    if not entries:
        return AreaTable((), (f"{FOLDER}: a pasta não tem nenhum arquivo de área",))
    problems: list[str] = []
    declared: list[tuple[str, Area]] = []
    for entry in entries:
        path = f"{FOLDER}/{entry.name}"
        if not entry.is_file() or entry.suffix != ".md":
            problems.append(f"{path}: não é um arquivo `.md` de área")
            continue
        area, found = parse_file(path, entry.read_text(encoding="utf-8").splitlines())
        problems.extend(found)
        if area is not None:
            declared.append((path, area))
            problems.extend(misnamed(path, area))
    problems.extend(duplicates(declared))
    return AreaTable(() if problems else tuple(a for _, a in declared), tuple(problems))
