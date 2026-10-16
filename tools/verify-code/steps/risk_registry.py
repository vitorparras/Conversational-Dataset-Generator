"""What reading the accepted-risks file produced: the risks and the problems found."""

from __future__ import annotations

from dataclasses import dataclass

from steps.accepted_risk import Risk


@dataclass(frozen=True)
class Registry:
    """The accepted risks of the file and the problems found while reading it."""

    risks: tuple[Risk, ...] = ()
    problems: tuple[str, ...] = ()
