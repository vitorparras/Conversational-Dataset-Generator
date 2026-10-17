"""One accepted risk: the finding it covers, the last day of the acceptance and the reason."""

from __future__ import annotations

from dataclasses import dataclass
from datetime import date


@dataclass(frozen=True)
class Risk:
    """An accepted finding: its id, what it hits, the last day it is accepted and why."""

    id: str
    subject: str
    expires: date
    reason: str
