"""One mutant as both tools report it: where it was placed, what it changed and how it ended."""

from __future__ import annotations

from dataclasses import dataclass
from enum import Enum


class MutantStatus(Enum):
    """How a mutant ended. Out of the share: NOT_COUNTED never compiled into a runnable mutant;
    IGNORED was switched off by a filter or by a comment in the code, and is shown as such."""

    KILLED = "killed"
    SURVIVED = "survived"
    NOT_COUNTED = "not_counted"
    IGNORED = "ignored"
    UNCHECKED = "unchecked"


@dataclass(frozen=True)
class Mutant:
    """A mutant's file (from the repository root), line, change and status."""

    file: str
    line: int
    change: str
    status: MutantStatus
