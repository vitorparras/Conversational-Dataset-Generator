"""Judges the coverage of each measured area against its floor in docs/operacao/014.

The floors are read from the document on every run; no number lives here. Code that no area owns
fails, and so does a production project or package of the tree that no area owns or no report read,
or a `.cs` file whose namespace and folder fall in different areas.
"""

from __future__ import annotations

import re
from collections.abc import Mapping
from dataclasses import dataclass
from pathlib import Path

from steps import namespace_folder
from steps.areas_doc import DOCUMENT, EXEMPT, WHOLE, Area, area_of, within
from steps.common import Context, StepResult, failed, passed
from steps.coverage_by_area import UNOWNED, Measurement, Tally, measure, rows, summarize
from steps.coverage_report import MeasuredLine
from steps.namespace_folder import BUILD_OUTPUT

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
    """A folder of production projects or packages, their unit prefix and the file marking one.

    ``code`` is the glob of its source files; ``nested`` says the first-level units are the folders
    of each project (C#), not the modules of the package itself (the service).
    """

    folder: str
    prefix: str
    marker: str
    code: str
    nested: bool


TREES = (
    Tree("core/src", "", "*.csproj", "*.cs", nested=True),
    Tree(
        "language-service/src/language_service",
        "language_service.",
        "__init__.py",
        "*.py",
        nested=False,
    ),
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


def holds_code(path: Path, tree: Tree) -> bool:
    """Whether a first-level entry is a source module or a folder with source below it."""
    if path.name in BUILD_OUTPUT or path.name.startswith("__"):
        return False
    return (
        any(path.rglob(tree.code)) if path.is_dir() else not tree.nested and path.match(tree.code)
    )


def entries(folder: Path) -> list[Path]:
    """The entries right under the folder, sorted; none when the folder does not exist."""
    return sorted(folder.iterdir()) if folder.is_dir() else []


def first_level(root: Path, tree: Tree) -> tuple[tuple[str, str], ...]:
    """The (path, unit) of each folder or module right under a project, or under the package."""
    parents = children(root, tree) if tree.nested else ("",)
    return tuple(
        (path.relative_to(root).as_posix() + "/" * path.is_dir(), f"{unit}.{path.stem}")
        for parent in parents
        for unit in (f"{tree.prefix}{parent}".rstrip("."),)
        for path in entries(root / tree.folder / parent)
        if holds_code(path, tree)
    )


def unlisted(root: Path, areas: tuple[Area, ...]) -> Verdict:
    """A failure for each first-level folder or module that has no row of its own in the document
    and that the row owning it does not enumerate after `Absorve:`, and for each name a row
    enumerates that is no longer on disk."""
    found = tuple(item for tree in TREES for item in first_level(root, tree))
    # A project or package no row owns is named by `orphans`, not here.
    owned = ((path, unit, area_of(unit, areas)) for path, unit in found)
    missing = (
        (path, owner)
        for path, unit, owner in owned
        if owner is not None
        and owner.prefix != unit
        and unit.rsplit(".", 1)[1] not in owner.absorbs
    )
    rule = f"sem linha em {DOCUMENT} nem enumerada em `Absorve:` na linha"
    on_disk = {unit for _, unit in found}
    gone = (
        (name, area)
        for area in areas
        for name in area.absorbs
        if f"{area.prefix}.{name}" not in on_disk
    )
    stale = "e não existe mais; tire-o da lista"
    return Verdict(
        failures=(
            *(f"{path}: {rule} `{owner.prefix}`" for path, owner in missing),
            *(
                f"{DOCUMENT}: `{name}` em `Absorve:` de `{area.prefix}` {stale}"
                for name, area in gone
            ),
        )
    )


def run(ctx: Context) -> StepResult:
    found = measure(ctx)
    misplaced = Verdict(namespace_folder.check(ctx.root))
    if isinstance(found, tuple):
        return failed(NAME, *found, *misplaced.failures)
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
        unlisted(ctx.root, found.areas),
        misplaced,
    )
    failures = tuple(line for verdict in verdicts for line in verdict.failures)
    notes = tuple(line for verdict in verdicts for line in verdict.notes)
    title = f"### Pisos de cobertura — {found.languages}"
    summarize("\n".join((title, "", *(f"- {line}" for line in (*failures, *notes)), "")))
    return (failed if failures else passed)(NAME, *failures, *notes)
