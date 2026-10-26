"""Reads the coverage areas from the table of docs/operacao/014, the only copy of the floors."""

from __future__ import annotations

import re
from dataclasses import dataclass
from itertools import takewhile
from pathlib import Path

DOCUMENT = "docs/operacao/014-modulos-criticos-e-pisos.md"
HEADER = "| Área | Projeto/pacote | Piso | Crítica | Motivo |"
EXEMPT = "isento"
YES = "sim"
NO = "não"
FLOOR = re.compile(r"(\d{1,3}) ?%")
PREFIX = re.compile(r"`([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)`")
SEPARATOR = re.compile(r"\|(?: *:?-+:? *\|){5}")
CELLS = 5
WHOLE = 100


@dataclass(frozen=True)
class Area:
    """One row of the table: a name, the prefix it owns, its floor (absent when exempt) and
    whether it is a critical module, the list the mutation test runs over."""

    name: str
    prefix: str
    floor: int | None
    critical: bool = False


@dataclass(frozen=True)
class AreaTable:
    """The areas of the document, or the problems that keep it from being read."""

    areas: tuple[Area, ...]
    problems: tuple[str, ...]


@dataclass(frozen=True)
class Row:
    """A table line of the document and its 1-based number."""

    number: int
    text: str


def within(unit: str, prefix: str) -> bool:
    """Whether ``unit`` is ``prefix`` or sits under it, segment by segment."""
    return unit == prefix or unit.startswith(f"{prefix}.")


def area_of(unit: str, areas: tuple[Area, ...]) -> Area | None:
    """The area with the longest prefix that owns ``unit``, or None when no area does."""
    owners = [area for area in areas if within(unit, area.prefix)]
    return max(owners, key=lambda area: len(area.prefix), default=None)


def table_rows(lines: list[str]) -> tuple[Row, ...] | None:
    """The lines of the table under the fixed header, or None when the header is absent."""
    stripped = [line.strip() for line in lines]
    if HEADER not in stripped:
        return None
    after = (Row(n + 1, stripped[n]) for n in range(stripped.index(HEADER) + 1, len(stripped)))
    return tuple(takewhile(lambda row: row.text.startswith("|"), after))


def parse_row(row: Row) -> Area | str:
    """The area a row declares, or the problem with it, naming the line."""
    where = f"{DOCUMENT}:{row.number}"
    cells = [cell.strip() for cell in row.text.strip("|").split("|")]
    if len(cells) != CELLS:
        return f"{where}: a linha tem {len(cells)} colunas; o cabeçalho fixo tem {CELLS}"
    name, prefix, floor, critical, _ = cells
    owner = PREFIX.fullmatch(prefix)
    if not name or owner is None:
        return f"{where}: área sem nome ou projeto/pacote '{prefix}' fora da forma `A.B` com crases"
    if critical not in (YES, NO):
        return f"{where}: Crítica '{critical}' não é `{YES}` nem `{NO}`"
    marked = critical == YES
    if floor == EXEMPT:
        return Area(name, owner.group(1), None, marked)
    number = FLOOR.fullmatch(floor)
    if number is None or int(number.group(1)) > WHOLE:
        return f"{where}: piso '{floor}' não é porcentagem de 0 a 100 (`90 %`) nem `{EXEMPT}`"
    return Area(name, owner.group(1), int(number.group(1)), marked)


def duplicates(rows: tuple[Row, ...], areas: list[Area]) -> list[str]:
    """A problem for each row whose area name or project/package an earlier row declared."""
    first: dict[tuple[int, str], int] = {}
    found: list[str] = []
    keys = (enumerate((area.name, area.prefix)) for area in areas)
    for row, key in ((r, k) for r, ks in zip(rows, keys, strict=True) for k in ks):
        if key in first:
            found.append(f"{DOCUMENT}:{row.number}: `{key[1]}` já aparece na linha {first[key]}")
        first.setdefault(key, row.number)
    return found


def read_areas(root: Path) -> AreaTable:
    """The area table of the document under ``root``; any malformed line fails the whole read."""
    path = root / DOCUMENT
    if not path.is_file():
        return AreaTable((), (f"{DOCUMENT} não existe: não há áreas para medir",))
    rows = table_rows(path.read_text(encoding="utf-8").splitlines())
    if rows is None:
        return AreaTable((), (f"{DOCUMENT}: cabeçalho fixo `{HEADER}` não encontrado",))
    if not rows or SEPARATOR.fullmatch(rows[0].text) is None:
        return AreaTable((), (f"{DOCUMENT}: falta a linha `|---|` logo abaixo do cabeçalho",))
    if len(rows) == 1:
        return AreaTable((), (f"{DOCUMENT}: a tabela de áreas não tem nenhuma linha",))
    parsed = [parse_row(row) for row in rows[1:]]
    problems = [p for p in parsed if isinstance(p, str)]
    if problems:
        return AreaTable((), tuple(problems))
    areas = [a for a in parsed if isinstance(a, Area)]
    repeated = duplicates(rows[1:], areas)
    return AreaTable(() if repeated else tuple(areas), tuple(repeated))
