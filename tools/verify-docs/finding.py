"""The problem record shared by every check, and the place that anchors it."""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path


@dataclass(frozen=True)
class Location:
    """A line of a document, with the root the document path is shown relative to."""

    root: Path
    path: Path
    line: int


@dataclass(frozen=True)
class Finding:
    """One problem found, anchored to a file (relative to the root) and a line."""

    file: Path
    line: int
    check: str
    message: str

    @classmethod
    def at(cls, location: Location, check: str, message: str) -> Finding:
        return cls(location.path.relative_to(location.root), location.line, check, message)

    def __str__(self) -> str:
        return f"{self.file.as_posix()}:{self.line}  [{self.check}] {self.message}"
