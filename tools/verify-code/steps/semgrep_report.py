"""Reads the JSON report of a semgrep scan: what was analyzed, what was not, and what was found."""

from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path
from typing import Any, cast

PARTIAL = "PartialParsing"


@dataclass(frozen=True)
class Finding:
    """A match: the rule id without the prefix semgrep adds, the file and the match's first line."""

    rule: str
    path: str
    line: int


def parse(text: str) -> dict[str, Any] | None:
    """The JSON object in ``text``, or None when there is none."""
    try:
        data = json.loads(text)
    except json.JSONDecodeError:
        return None
    return cast("dict[str, Any]", data) if isinstance(data, dict) else None


def read(report: Path) -> dict[str, Any] | None:
    """The scan report, or None when it is missing, malformed or lacks results and paths."""
    data = parse(report.read_text(encoding="utf-8")) if report.is_file() else None
    return data if data is not None and {"results", "paths"} <= data.keys() else None


def items(container: dict[str, Any], key: str) -> list[dict[str, Any]]:
    return cast("list[dict[str, Any]]", container.get(key) or [])


def scanned(report: dict[str, Any]) -> tuple[str, ...]:
    paths = cast("dict[str, Any]", report.get("paths") or {})
    return tuple(cast("list[str]", paths.get("scanned") or []))


def findings(report: dict[str, Any]) -> tuple[Finding, ...]:
    """Every match, with the rule id stripped of the folder prefix semgrep puts before it."""
    return tuple(
        Finding(str(r["check_id"]).rsplit(".", 1)[-1], str(r["path"]), int(r["start"]["line"]))
        for r in items(report, "results")
    )


def kind_of(error: dict[str, Any]) -> str:
    """The error kind: a plain name, or the first item of a kind that carries spans."""
    kind = cast("object", error.get("type"))
    return str(cast("list[object]", kind)[0]) if isinstance(kind, list) else str(kind)


def spans(error: dict[str, Any]) -> list[dict[str, Any]]:
    """The unparsed spans of a partial parsing error; empty for any other kind of error."""
    if kind_of(error) != PARTIAL:
        return []
    return cast("list[dict[str, Any]]", error["type"][1])


def span_lines(unparsed: list[dict[str, Any]]) -> set[int]:
    return {n for span in unparsed for n in range(span["start"]["line"], span["end"]["line"] + 1)}


def covered(text: str, unparsed: list[dict[str, Any]]) -> bool:
    """Whether the unparsed spans cover every line of the file that has content."""
    lines = span_lines(unparsed)
    content = {n for n, line in enumerate(text.splitlines(), start=1) if line.strip()}
    return content <= lines


def fatal(error: dict[str, Any], root: Path) -> bool:
    """Any error but a partial parse fails, and so does a partial parse of the whole file."""
    unparsed = spans(error)
    if not unparsed:
        return True
    text = (root / str(error["path"])).read_text(encoding="utf-8", errors="replace")
    return covered(text, unparsed)


def describe(error: dict[str, Any], root: Path) -> str:
    """The report line of one error: what was left unanalyzed, by file and lines."""
    path = str(error.get("path") or "")
    unparsed = spans(error)
    if not unparsed:
        return f"erro do semgrep em {path or 'uma regra'}: {kind_of(error)}"
    if fatal(error, root):
        return f"{path}: arquivo não analisado, o semgrep não consegue lê-lo"
    lines = sorted({(int(s["start"]["line"]), int(s["end"]["line"])) for s in unparsed})
    ranges = ", ".join(str(a) if a == b else f"{a}-{b}" for a, b in lines)
    return f"trecho não analisado: {path}:{ranges}"
