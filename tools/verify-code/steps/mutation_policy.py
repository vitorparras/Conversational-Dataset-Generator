"""Reads the mutation rules of docs/operacao/014: which floor marks a module as critical, the share
of survivors a module may keep and the time ceiling per language. No number lives here."""

from __future__ import annotations

import re
from dataclasses import dataclass
from itertools import takewhile
from pathlib import Path

from steps.areas_doc import DOCUMENT, Area

SECTION = "## Mutação"
PHRASES = (
    (re.compile(r"áreas de piso (\d{1,3}) %"), "áreas de piso N %"),
    (
        re.compile(r"no máximo \*\*(\d{1,3}) % dos mutantes gerados\*\*"),
        "no máximo **N % dos mutantes gerados**",
    ),
    (re.compile(r"\*\*(\d{1,3}) minutos por linguagem\*\*"), "**N minutos por linguagem**"),
)


@dataclass(frozen=True)
class MutationPolicy:
    """The floor of a critical area, the survivor ceiling in percent, the minutes per language."""

    critical_floor: int
    survivor_ceiling: int
    minutes: int


def section(lines: list[str]) -> str | None:
    """The text of the mutation section on one line, or None when the heading is absent."""
    stripped = [line.strip() for line in lines]
    if SECTION not in stripped:
        return None
    after = stripped[stripped.index(SECTION) + 1 :]
    return " ".join(takewhile(lambda line: not line.startswith("## "), after))


def read_policy(root: Path) -> MutationPolicy | tuple[str, ...]:
    """The three numbers of the section, or the phrases the document no longer carries."""
    path = root / DOCUMENT
    text = section(path.read_text(encoding="utf-8").splitlines()) if path.is_file() else None
    if text is None:
        return (f"{DOCUMENT}: seção `{SECTION}` não encontrada; não há regra de mutação",)
    found = [pattern.search(text) for pattern, _ in PHRASES]
    missing = [phrase for (_, phrase), match in zip(PHRASES, found, strict=True) if match is None]
    if missing:
        return tuple(f"{DOCUMENT}: a seção `{SECTION}` não diz `{phrase}`" for phrase in missing)
    return MutationPolicy(*(int(match.group(1)) for match in found if match is not None))


def critical(areas: tuple[Area, ...], policy: MutationPolicy) -> tuple[Area, ...]:
    """The areas whose floor is the one the section names: the list of critical modules."""
    return tuple(area for area in areas if area.floor == policy.critical_floor)


def above(areas: tuple[Area, ...], policy: MutationPolicy) -> tuple[str, ...]:
    """A problem per area whose floor passes the critical one: whether it stays critical is open."""
    return tuple(
        f"{area.name} (`{area.prefix}`): piso {area.floor} % acima do piso crítico de "
        f"{policy.critical_floor} %; {DOCUMENT} não diz se a área continua crítica"
        for area in areas
        if area.floor is not None and area.floor > policy.critical_floor
    )
