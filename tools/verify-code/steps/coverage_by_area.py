"""Publishes line coverage per area of docs/operacao/014 from the reports the test steps wrote.

Only the languages whose report exists are listed, so each CI job runs it after its own suite.
No floor is judged here: code no area owns is listed under its own row for the step that does.
"""

from __future__ import annotations

import os
from collections.abc import Callable, Iterable
from dataclasses import dataclass
from functools import partial
from pathlib import Path
from xml.etree import ElementTree

from steps import test_core, test_real_service, test_service, test_tools
from steps.areas_doc import EXEMPT, Area, area_of, read_areas, within
from steps.common import OUTPUT_DIRECTORY, Context, StepResult, failed, passed
from steps.coverage_report import MeasuredLine, cobertura, coverage_py

NAME = "coverage"
RESULTS = "coverage"
TABLE = "by-area.md"
SUMMARY = "GITHUB_STEP_SUMMARY"
UNOWNED = "(sem área)"
HEADER = ("| Área | Piso | Medido | Linhas |", "|---|---|---|---|")
EMPTY = "rode antes test_core, test_service ou test_tools; o passo não passa vazio"


@dataclass(frozen=True)
class Report:
    """A language's coverage report: its name, the prefix root it owns, where and how to read it;
    the patterns are the folders of the steps that wrote one, merged line by line."""

    language: str
    root: str
    patterns: tuple[str, ...]
    read: Callable[[Path, Path], tuple[MeasuredLine, ...]]


@dataclass(frozen=True)
class Tally:
    """Coverable lines of one row of the table and how many of them ran."""

    label: str
    floor: str
    covered: int
    coverable: int

    def percent(self) -> str:
        share = 100 * self.covered / self.coverable if self.coverable else 0.0
        return f"{share:.1f} %".replace(".", ",")

    def line(self) -> str:
        lines = f"{self.covered} de {self.coverable}"
        return f"| {self.label} | {self.floor} | {self.percent()} | {lines} |"


@dataclass(frozen=True)
class Measurement:
    """Every area, those of the languages whose report was read, their lines and those languages."""

    areas: tuple[Area, ...]
    measured: tuple[Area, ...]
    lines: tuple[MeasuredLine, ...]
    languages: str


REPORTS: tuple[Report, ...] = (
    Report(
        "núcleo (.NET)",
        "Core",
        (
            f"{test_core.RESULTS}/*.cobertura.xml",
            f"{test_real_service.RESULTS}/*.cobertura.xml",
        ),
        lambda path, _root: cobertura(path),
    ),
    Report(
        "serviço (Python)",
        "language_service",
        (f"{test_service.RESULTS}/{test_service.REPORT}",),
        partial(coverage_py, segment="language_service"),
    ),
    Report(
        "ferramentas",
        test_tools.TOOLS,
        (f"{test_tools.RESULTS}/{test_tools.REPORT}",),
        partial(coverage_py, segment=test_tools.TOOLS),
    ),
)


def merged(lines: Iterable[MeasuredLine]) -> tuple[MeasuredLine, ...]:
    """One entry per file line; a line counts as run when any report saw it run."""
    by_place: dict[tuple[str, int], MeasuredLine] = {}
    for line in lines:
        earlier = by_place.get((line.file, line.number))
        by_place[(line.file, line.number)] = earlier if earlier and earlier.hit else line
    return tuple(by_place.values())


def floor_of(area: Area) -> str:
    return EXEMPT if area.floor is None else f"{area.floor} %"


def tally(label: str, floor: str, lines: list[MeasuredLine]) -> Tally:
    return Tally(label, floor, sum(line.hit for line in lines), len(lines))


def rows(
    lines: tuple[MeasuredLine, ...], areas: tuple[Area, ...], measured: tuple[Area, ...]
) -> tuple[Tally, ...]:
    """A row per area of a measured language, zero when nothing reached it, then unowned code."""
    owner = {line: area_of(line.unit, areas) for line in lines}
    found = [
        tally(area.name, floor_of(area), [line for line in lines if owner[line] == area])
        for area in measured
    ]
    unowned = [line for line in lines if owner[line] is None]
    return (*found, tally(UNOWNED, "—", unowned)) if unowned else tuple(found)


def publish(ctx: Context, text: str) -> None:
    """Writes the table under the output directory and appends it to the CI job summary."""
    target = ctx.out_dir / RESULTS
    target.mkdir(parents=True, exist_ok=True)
    (target / TABLE).write_text(text, encoding="utf-8", newline="\n")
    summarize(text)


def summarize(text: str) -> None:
    """Appends ``text`` to the CI job summary, when the job names one."""
    summary = os.environ.get(SUMMARY)
    if summary:
        with Path(summary).open("a", encoding="utf-8", newline="\n") as stream:
            stream.write(text)


def _present(ctx: Context) -> tuple[tuple[Report, tuple[Path, ...]], ...]:
    """The reports this run finds on disk, each with its files."""
    found = (
        (report, tuple(sorted(p for pattern in report.patterns for p in ctx.out_dir.glob(pattern))))
        for report in REPORTS
    )
    return tuple((report, paths) for report, paths in found if paths)


def measure(ctx: Context) -> Measurement | tuple[str, ...]:
    """The lines of the reports on disk and the areas they reach, or why they cannot be read."""
    table = read_areas(ctx.root)
    if table.problems:
        return table.problems
    reports = _present(ctx)
    if not reports:
        return (f"nenhum relatório de cobertura em {OUTPUT_DIRECTORY}/: {EMPTY}",)
    try:
        lines = merged(line for r, paths in reports for p in paths for line in r.read(p, ctx.root))
    except ElementTree.ParseError as error:
        return (f"relatório de cobertura ilegível: {error}",)
    measured = tuple(a for a in table.areas if any(within(a.prefix, r.root) for r, _ in reports))
    languages = ", ".join(report.language for report, _ in reports)
    return Measurement(table.areas, measured, lines, languages)


def run(ctx: Context) -> StepResult:
    found = measure(ctx)
    if isinstance(found, tuple):
        return failed(NAME, *found)
    tallies = rows(found.lines, found.areas, found.measured)
    title = f"### Cobertura por área — {found.languages}"
    text = "\n".join((title, "", *HEADER, *(t.line() for t in tallies)))
    publish(ctx, f"{text}\n")
    return passed(NAME, *text.splitlines(), f"tabela em {OUTPUT_DIRECTORY}/{RESULTS}/{TABLE}")
