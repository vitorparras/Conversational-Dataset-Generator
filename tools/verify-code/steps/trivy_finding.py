"""One HIGH or CRITICAL result of a Trivy scan."""

from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class Finding:
    """What a finding is, what it hits and where."""

    kind: str
    id: str
    subject: str
    target: str
    severity: str
