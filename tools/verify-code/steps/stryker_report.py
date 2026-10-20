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
    "Ignored": MutantStatus.NOT_COUNTED,
}


def relative(file: str, root: Path) -> str:
    """The report's file path from the repository root, when it lies under it."""
    path = Path(file)
    return path.relative_to(root).as_posix() if path.is_relative_to(root) else path.as_posix()


def mutant(file: str, entry: dict[str, Any]) -> Mutant:
    """One mutant of the report; a status the table does not know never ran (Pending)."""
    line = int(entry["location"]["start"]["line"])
    change = f"{entry['mutatorName']}: `{entry.get('replacement', '')}`"
    return Mutant(file, line, change, STATUS.get(entry["status"], MutantStatus.UNCHECKED))


def read_stryker(path: Path, root: Path) -> tuple[Mutant, ...]:
    """Every mutant of every file of the report, files named from ``root``."""
    files: dict[str, Any] = json.loads(path.read_text(encoding="utf-8"))["files"]
    return tuple(
        mutant(relative(file, root), entry)
        for file, content in files.items()
        for entry in content["mutants"]
    )
