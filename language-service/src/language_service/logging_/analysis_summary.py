"""What the log may say about a finished analysis: sizes, times and counts."""

from collections.abc import Mapping
from dataclasses import dataclass


@dataclass(frozen=True)
class AnalysisSummary:
    """The reference of the request, the size of its text and what the analysis found, by count."""

    reference: str
    chars: int
    elapsed_ms: int
    model_version: str | None
    count_by_kind: Mapping[str, int]
