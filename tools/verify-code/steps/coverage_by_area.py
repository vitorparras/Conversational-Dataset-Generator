"""Coverage per area, from the reports of both suites, cut the way the floors document cuts it.

Reads Cobertura XML (coverlet, .NET) and coverage.py XML (Python), counts each line once taking the
union of what the reports executed, and counts lines per area. An area nothing reaches is
listed with zero, never left out: the silent failure of a coverage report is the area that vanishes.
The numbers are line coverage. The output is deterministic: the same reports give the same bytes.
"""

from __future__ import annotations

import json
import shutil
from dataclasses import dataclass
from pathlib import Path

from steps.areas_doc import AreaRow, Document, read_document, repository_path
from steps.common import FLOORS_DOCUMENT, Context, StepResult, local_name, parse_xml

NAME = "coverage_by_area"
GROUP = "report"
DESCRIPTION = "agrega a cobertura por área conforme o documento de pisos"

REPORT_FILE = "coverage-by-area.json"
PREVIOUS_FILE = "coverage-by-area.previous.json"

MEASURED = "measured"
ABSENT = "absent"
UNMEASURED = "unmeasured"


@dataclass
class Entry:
    """The lines of one source file (or class) and how many times each ran."""

    class_name: str | None
    path: str | None
    lines: dict[int, int]


@dataclass(frozen=True)
class Measurement:
    """The coverage of one area."""

    area: str
    project: str
    covered: int
    total: int
    status: str

    @property
    def percent(self) -> float:
        return round(100 * self.covered / self.total, 2) if self.total else 0.0

    def as_json(self) -> dict[str, object]:
        return {
            "area": self.area,
            "project": self.project,
            "covered": self.covered,
            "total": self.total,
            "percent": self.percent,
            "status": self.status,
        }


def locate(sources: list[Path], filename: str, root: Path) -> str | None:
    """The repository path of a file a report names relative to one of its ``<source>`` folders."""
    if not filename:
        return None
    full = next((s / filename for s in sources if (s / filename).exists()), None)
    if full is None:
        full = sources[0] / filename if sources else Path(filename)
    return repository_path(str(full), root)


def read_cobertura(path: Path, root: Path) -> list[Entry]:
    """Classes of a Cobertura report (coverlet): their name, file and executed lines."""
    document = parse_xml(path)
    sources = [Path(e.text.strip()) for e in document.iter() if local_name(e.tag) == "source" and e.text]
    entries: list[Entry] = []
    for element in document.iter():
        if local_name(element.tag) != "class":
            continue
        lines = {
            int(line.get("number", "0")): int(line.get("hits", "0"))
            for line in element.iter()
            if local_name(line.tag) == "line" and line.get("number")
        }
        entries.append(Entry(element.get("name", ""), locate(sources, element.get("filename", ""), root), lines))
    return entries


def read_coverage_py(path: Path, root: Path) -> list[Entry]:
    """Files of a coverage.py XML report: their repository path and executed lines."""
    document = parse_xml(path)
    sources = [Path(source.text.strip()) for source in document.iter("source") if source.text]
    entries: list[Entry] = []
    for element in document.iter("class"):
        lines = {int(line.get("number", "0")): int(line.get("hits", "0")) for line in element.iter("line")}
        entries.append(Entry(None, locate(sources, element.get("filename", ""), root), lines))
    return entries


def owner(rows: list[AreaRow], entry: Entry) -> AreaRow | None:
    """The most specific area an entry belongs to, or None."""
    best: tuple[int, AreaRow] | None = None
    for row in rows:
        specificity = len(row.source_path)
        if row.kind == "dotnet" and entry.class_name is not None:
            name = entry.class_name
            if name == row.project or name.startswith((row.project + ".", row.project + "/")):
                candidate = (specificity, row)
                best = candidate if best is None or candidate[0] > best[0] else best
        if entry.path is not None and (
            entry.path.startswith(row.source_path + "/")
            or (row.kind == "python" and entry.path == row.source_path + ".py")
        ):
            candidate = (specificity, row)
            best = candidate if best is None or candidate[0] > best[0] else best
    return best[1] if best else None


def aggregate(rows: list[AreaRow], entries: list[Entry], root: Path) -> list[Measurement]:
    """Count the lines of every area, in the order of the document; an unreached area is zero.

    A line is identified by the file it is in (the class, when the report names no file) and its
    number, so the same line in several reports, or in a class and in the closure inside it, counts
    once, and as covered if any report ran it.
    """
    hits: dict[tuple[str, str, int], int] = {}
    for entry in entries:
        row = owner(rows, entry)
        if row is None:
            continue
        place = entry.path if entry.path is not None else str(entry.class_name)
        for number, count in entry.lines.items():
            key = (row.area, place, number)
            hits[key] = max(hits.get(key, 0), count)
    counts: dict[str, tuple[int, int]] = {row.area: (0, 0) for row in rows}
    for (area, _, _), count in hits.items():
        covered, total = counts[area]
        counts[area] = (covered + (1 if count > 0 else 0), total + 1)
    measurements: list[Measurement] = []
    for row in rows:
        covered, total = counts[row.area]
        if total:
            status = MEASURED
        elif not row.exists(root):
            status = ABSENT
        else:
            status = UNMEASURED
        measurements.append(Measurement(row.area, row.project, covered, total, status))
    return measurements


def render_table(measurements: list[Measurement]) -> list[str]:
    """The measurements as a markdown table, the same text for the console and the job summary."""
    lines = ["| Área | Projeto/pacote | Linhas | Cobertura |", "|---|---|---|---|"]
    for m in measurements:
        shown = f"{m.percent:.2f}%" if m.status == MEASURED else {
            ABSENT: "0.00% (a área ainda não existe)",
            UNMEASURED: "0.00% (existe e nenhuma linha foi medida)",
        }[m.status]
        lines.append(f"| {m.area} | {m.project} | {m.covered}/{m.total} | {shown} |")
    return lines


def to_json(measurements: list[Measurement]) -> str:
    """The report for the next comparison. Deterministic: no timestamp, fixed order and formatting."""
    return json.dumps({"version": 1, "areas": [m.as_json() for m in measurements]}, indent=2) + "\n"


def collect(ctx: Context, document: Document, result: StepResult) -> list[Entry] | None:
    """Read every report the test steps left; a kind of area with no report at all is a failure."""
    cobertura = sorted((ctx.out_dir / "core").rglob("coverage.cobertura.xml"))
    python = sorted((ctx.out_dir / "service").glob("coverage.xml")) + sorted(
        (ctx.out_dir / "tools").glob("coverage.xml")
    )
    kinds = {row.kind for row in document.areas}
    missing = [
        label
        for label, kind, files in (
            ("núcleo (Cobertura)", "dotnet", cobertura),
            ("serviço (coverage.py)", "python", sorted((ctx.out_dir / "service").glob("coverage.xml"))),
            ("tools (coverage.py)", "path", sorted((ctx.out_dir / "tools").glob("coverage.xml"))),
        )
        if kind in kinds and not files
    ]
    if missing:
        result.fail("falta o relatório de cobertura de: " + ", ".join(missing) + ". Rode os passos de teste antes")
        return None
    entries: list[Entry] = []
    for report in cobertura:
        entries.extend(read_cobertura(report, ctx.root))
    for report in python:
        entries.extend(read_coverage_py(report, ctx.root))
    return entries


def run(ctx: Context) -> StepResult:
    result = StepResult()
    document = read_document(ctx.root / FLOORS_DOCUMENT)
    if document.problems:
        for problem in document.problems:
            result.fail(f"{FLOORS_DOCUMENT}: {problem}")
        return result
    entries = collect(ctx, document, result)
    if entries is None:
        return result
    measurements = aggregate(document.areas, entries, ctx.root)
    table = render_table(measurements)
    result.lines.extend(table)

    report = ctx.out_dir / REPORT_FILE
    if report.is_file():
        shutil.copyfile(report, ctx.out_dir / PREVIOUS_FILE)
    report.write_text(to_json(measurements), encoding="utf-8", newline="\n")

    if ctx.summary_path is not None:
        with ctx.summary_path.open("a", encoding="utf-8", newline="\n") as summary:
            summary.write("### Cobertura por área\n\n" + "\n".join(table) + "\n\n")
    return result
