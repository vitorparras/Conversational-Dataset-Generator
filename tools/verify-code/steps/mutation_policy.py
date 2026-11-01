"""Reads the mutation rules of docs/operacao/017: the share of survivors a module may keep, the
time ceiling per module and the method whose `false` argument is left out; the critical modules
are the Crítica column of the files of docs/operacao/areas. No number lives here."""

from __future__ import annotations

import re
from dataclasses import dataclass
from itertools import takewhile
from pathlib import Path

from steps.areas_doc import Area

DOCUMENT = "docs/operacao/017-teste-de-mutacao.md"
SECTION = "## Regras"
PHRASES = (
    (
        re.compile(r"no máximo \*\*(\d{1,3}) % dos mutantes gerados\*\*"),
        "no máximo **N % dos mutantes gerados**",
    ),
    (re.compile(r"\*\*(\d{1,3}) minutos por módulo\*\*"), "**N minutos por módulo**"),
    (
        re.compile(r"\*\*o mutante `false`→`true` do argumento de `(\w+)`\*\*"),
        "**o mutante `false`→`true` do argumento de `Método`**",
    ),
)


@dataclass(frozen=True)
class MutationPolicy:
    """The survivor ceiling in percent, the minutes per module and the method whose `false`
    argument is left out of the share."""

    survivor_ceiling: int
    minutes: int
    skipped_argument_of: str


def section(lines: list[str]) -> str | None:
    """The text of the mutation section on one line, or None when the heading is absent."""
    stripped = [line.strip() for line in lines]
    if SECTION not in stripped:
        return None
    after = stripped[stripped.index(SECTION) + 1 :]
    return " ".join(takewhile(lambda line: not line.startswith("## "), after))


def read_policy(root: Path) -> MutationPolicy | tuple[str, ...]:
    """The two numbers of the section, or the phrases the document no longer carries."""
    path = root / DOCUMENT
    text = section(path.read_text(encoding="utf-8").splitlines()) if path.is_file() else None
    if text is None:
        return (f"{DOCUMENT}: seção `{SECTION}` não encontrada; não há regra de mutação",)
    found = [pattern.search(text) for pattern, _ in PHRASES]
    missing = [phrase for (_, phrase), match in zip(PHRASES, found, strict=True) if match is None]
    if missing:
        return tuple(f"{DOCUMENT}: a seção `{SECTION}` não diz `{phrase}`" for phrase in missing)
    ceiling, minutes, method = (match.group(1) for match in found if match is not None)
    return MutationPolicy(int(ceiling), int(minutes), method)


def critical(areas: tuple[Area, ...]) -> tuple[Area, ...]:
    """The areas the Crítica column marks `sim`: the critical modules, whatever their floor."""
    return tuple(area for area in areas if area.critical)
