"""One mutant as both tools report it: where it was placed, what it changed and how it ended."""

from __future__ import annotations

from dataclasses import dataclass
from enum import Enum


class MutantStatus(Enum):
    """How a mutant ended: a test caught it, none did, it never counted, or it never ran."""

    KILLED = "killed"
    SURVIVED = "survived"
    NOT_COUNTED = "not_counted"
    UNCHECKED = "unchecked"


@dataclass(frozen=True)
class Mutant:
    """A mutant's file (from the repository root), line, change and status."""

    file: str
    line: int
    change: str
    status: MutantStatus
