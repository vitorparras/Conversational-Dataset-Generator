"""Judges the coverage of each measured area against its floor in docs/operacao/014.

The floors are read from the document on every run; no number lives here. Code that no area owns
fails, and so does a production project or package of the tree that no area owns or no report read.
"""

from __future__ import annotations

import re
from collections.abc import Mapping
from dataclasses import dataclass
from pathlib import Path

from steps.areas_doc import DOCUMENT, EXEMPT, WHOLE, Area, area_of, within
from steps.common import Context, StepResult, failed, passed
from steps.coverage_by_area import UNOWNED, Measurement, Tally, measure, rows, summarize
from steps.coverage_report import MeasuredLine

NAME = "floors"
ROW = re.compile(r"\| (.+?) \| (.+?) \| [\d,]+ % \| (\d+) de (\d+) \|")
NO_PREVIOUS = "sem medição anterior para comparar (--previous <by-area.md de outra execução>)"


@dataclass(frozen=True)
class Verdict:
    """What one part of the judgement prints: lines that fail the step and lines that inform."""

    failures: tuple[str, ...] = ()
    notes: tuple[str, ...] = ()


@dataclass(frozen=True)
class Tree:
    """A folder of production projects or packages, their unit prefix and the file marking one."""

    folder: str
    prefix: str
    marker: str


TREES = (
    Tree("core/src", "", "*.csproj"),
    Tree("language-service/src/language_service", "language_service.", "__init__.py"),
)


def truncated(tally: Tally) -> str:
    """The share cut, not rounded, to one decimal, so a share under the floor never reads as it."""
    return f"{tally.covered * 1000 // tally.coverable / 10:.1f} %".replace(".", ",")


def judge(area: Area, now: Tally, before: Tally | None, unmeasured: bool) -> Verdict:
    """The verdict of one measured area against its floor, warning of a drop that stays above it."""
    if area.floor is None:
        return Verdict(notes=(f"{area.name}: {EXEMPT}, nada se compara (medido {now.percent()})",))
    if now.coverable == 0 and unmeasured:
        missed = f"código sem medição: nenhum relatório cobre `{area.prefix}`"
        return Verdict(failures=(f"{area.name}: {missed}",))
    if now.coverable == 0:
        empty = f"sem código (0 de 0); o piso de {area.floor} % vale quando houver linha"
        return Verdict(notes=(f"{area.name}: {empty}",))
    if now.covered * WHOLE < area.floor * now.coverable:
        below = f"{area.name}: medido {truncated(now)}, abaixo do piso de {area.floor} %"
        return Verdict(failures=(f"{below} ({now.covered} de {now.coverable} linhas)",))
    met = f"{area.name}: {now.percent()} atende ao piso de {area.floor} %"
    if before is None or now.covered * before.coverable >= before.covered * now.coverable:
        return Verdict(notes=(met,))
    drop = f"caiu de {before.percent()} para {now.percent()} sem cruzar o piso de {area.floor} %"
    return Verdict(notes=(met, f"AVISO — {area.name} {drop}"))


def earlier(path: Path | None) -> Mapping[str, Tally]:
    """The rows of an earlier run's table by area name; empty when there is no such file or row."""
    text = path.read_text(encoding="utf-8") if path is not None and path.is_file() else ""
    found = (ROW.fullmatch(line.strip()) for line in text.splitlines())
    return {m[1]: Tally(m[1], m[2], int(m[3]), int(m[4])) for m in found if m}


def baseline(path: Path | None, before: Mapping[str, Tally]) -> Verdict:
    """What the drops were compared with; an earlier table with no row fails the step."""
    if path is None:
        return Verdict(notes=(NO_PREVIOUS,))
    if not path.is_file():
        return Verdict(failures=(f"--previous {path}: arquivo não existe",))
    if not before:
        return Verdict(failures=(f"--previous {path}: nenhuma linha de tabela de cobertura",))
    return Verdict(notes=(f"quedas comparadas com {path}",))


def unowned(found: Measurement) -> Verdict:
    """A failure naming the measured units no area owns: an absent floor is not a floor of zero."""
    units = sorted({line.unit for line in found.lines if area_of(line.unit, found.areas) is None})
    if not units:
        return Verdict()
    named = ", ".join(f"`{unit}`" for unit in units)
    return Verdict(failures=(f"{UNOWNED}: nenhuma área de {DOCUMENT} possui {named}",))


def children(root: Path, tree: Tree) -> tuple[str, ...]:
    """The folders right under the tree's folder that hold its marker file, by name."""
    folder = root / tree.folder
    found = folder.iterdir() if folder.is_dir() else iter(())
    return tuple(sorted(path.name for path in found if any(path.glob(tree.marker))))


def unread(root: Path, area: Area, lines: tuple[MeasuredLine, ...]) -> bool:
    """Whether the area is a project or package on disk of which no report read a single line."""
    on_disk = any(
        area.prefix.removeprefix(tree.prefix) in children(root, tree)
        for tree in TREES
        if area.prefix.startswith(tree.prefix)
    )
    return on_disk and not any(within(line.unit, area.prefix) for line in lines)


def orphans(root: Path, areas: tuple[Area, ...]) -> Verdict:
    """A failure for each production project or package that no area of the document owns."""
    missing = (
        f"{tree.folder}/{name}/"
        for tree in TREES
        for name in children(root, tree)
        if area_of(f"{tree.prefix}{name}", areas) is None
    )
    rule = f"sem área em {DOCUMENT}; a linha entra no mesmo pull request que o cria"
    return Verdict(failures=tuple(f"{folder}: {rule}" for folder in missing))


def run(ctx: Context) -> StepResult:
    found = measure(ctx)
    if isinstance(found, tuple):
        return failed(NAME, *found)
    before = earlier(ctx.previous)
    tallies = rows(found.lines, found.areas, found.measured)[: len(found.measured)]
    verdicts = (
        *(
            judge(area, tally, before.get(area.name), unread(ctx.root, area, found.lines))
            for area, tally in zip(found.measured, tallies, strict=True)
        ),
        baseline(ctx.previous, before),
        unowned(found),
        orphans(ctx.root, found.areas),
    )
    failures = tuple(line for verdict in verdicts for line in verdict.failures)
    notes = tuple(line for verdict in verdicts for line in verdict.notes)
    title = f"### Pisos de cobertura — {found.languages}"
    summarize("\n".join((title, "", *(f"- {line}" for line in (*failures, *notes)), "")))
    return (failed if failures else passed)(NAME, *failures, *notes)
