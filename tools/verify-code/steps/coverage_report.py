"""Reads the lines of a Cobertura report (core) or a coverage.py XML report (service, tools)."""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path, PurePath
from xml.etree import ElementTree

from steps.areas_doc import within


@dataclass(frozen=True)
class MeasuredLine:
    """One coverable line: the dotted unit an area matches, its file, its number, and if it ran."""

    unit: str
    file: str
    number: int
    hit: bool


def lines_of(element: ElementTree.Element, unit: str, file: str) -> tuple[MeasuredLine, ...]:
    """The class-level lines of a report's class; method-level copies of them are not counted."""
    return tuple(
        MeasuredLine(unit, file, int(line.get("number", "0")), int(line.get("hits", "0")) > 0)
        for line in element.findall("lines/line")
    )


def class_unit(package: str, name: str) -> str:
    """The namespace-qualified class; a class outside its assembly's namespace goes under it."""
    return name if within(name, package) else f"{package}.{name}"


def cobertura(path: Path) -> tuple[MeasuredLine, ...]:
    """Every line of a Cobertura report, each under its class's namespace."""
    found: list[MeasuredLine] = []
    for package in ElementTree.parse(path).getroot().iter("package"):
        for element in package.iter("class"):
            unit = class_unit(package.get("name", ""), element.get("name", ""))
            found.extend(lines_of(element, unit, element.get("filename", "")))
    return tuple(found)


def module_unit(file: PurePath, root: Path, segment: str) -> str:
    """The dotted module of a file, from its first folder named ``segment`` under ``root``."""
    parts = file.relative_to(root).with_suffix("").parts if file.is_relative_to(root) else ()
    start = parts.index(segment) if segment in parts else len(parts)
    return ".".join(parts[start:])


def coverage_py(path: Path, root: Path, segment: str) -> tuple[MeasuredLine, ...]:
    """Every line of a coverage.py XML report, each under its module's dotted name."""
    report = ElementTree.parse(path).getroot()
    source, base = Path(report.findtext("sources/source", "")), root.resolve()
    found: list[MeasuredLine] = []
    for element in report.iter("class"):
        file = source / element.get("filename", "")
        found.extend(lines_of(element, module_unit(file, base, segment), file.as_posix()))
    return tuple(found)
