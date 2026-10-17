"""The result of looking for a pinned binary: a verified path, or the problem in Portuguese."""

from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class Located:
    """The verified binary path, or the reason there is none."""

    path: str | None
    problem: str = ""
