"""Reads the mutants of a Stryker.NET JSON report (the mutation-testing-elements schema)."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from steps.mutant import Mutant, MutantStatus

STATUS = {
    "Killed": MutantStatus.KILLED,
    "Timeout": MutantStatus.KILLED,
    "Survived": MutantStatus.SURVIVED,
    "NoCoverage": MutantStatus.SURVIVED,
    "CompileError": MutantStatus.NOT_COUNTED,
    "RuntimeError": MutantStatus.NOT_COUNTED,
    "Ignored": MutantStatus.IGNORED,
}
BOOLEAN = "Boolean mutation"


def relative(file: str, root: Path) -> str:
    """The report's file path from the repository root, when it lies under it."""
    path = Path(file)
    return path.relative_to(root).as_posix() if path.is_relative_to(root) else path.as_posix()


def is_argument_of(entry: dict[str, Any], source: list[str], method: str) -> bool:
    """Whether the mutant flips the `false` argument of a call to ``method``: a Boolean mutation
    whose span is `false` and sits right after `method(`. The call around it is not touched."""
    start, end = entry["location"]["start"], entry["location"]["end"]
    line, column, stop = int(start["line"]), int(start["column"]) - 1, int(end["column"]) - 1
    if entry["mutatorName"] != BOOLEAN or line != int(end["line"]) or line > len(source):
        return False
    text = source[line - 1]
    return text[column:stop] == "false" and text[:column].rstrip().endswith(f"{method}(")


def mutant(file: str, entry: dict[str, Any], skipped: bool = False) -> Mutant:
    """One mutant of the report; a status the table does not know never ran (Pending)."""
    line = int(entry["location"]["start"]["line"])
    change = f"{entry['mutatorName']}: `{entry.get('replacement', '')}`"
    status = STATUS.get(entry["status"], MutantStatus.UNCHECKED)
    return Mutant(file, line, change, MutantStatus.IGNORED if skipped else status)


def read_stryker(path: Path, root: Path, argument_of: str | None = None) -> tuple[Mutant, ...]:
    """Every mutant of every file of the report, files named from ``root``. With ``argument_of``,
    the mutant that flips the `false` argument of that method is shown as ignored, whatever Stryker
    made of it; every other mutant on the same line still counts."""
    files: dict[str, Any] = json.loads(path.read_text(encoding="utf-8"))["files"]
    found: list[Mutant] = []
    for file, content in files.items():
        source = content.get("source", "").split("\n")
        for entry in content["mutants"]:
            skipped = argument_of is not None and is_argument_of(entry, source, argument_of)
            found.append(mutant(relative(file, root), entry, skipped))
    return tuple(found)
