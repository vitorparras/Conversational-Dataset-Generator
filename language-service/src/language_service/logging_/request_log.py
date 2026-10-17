"""The log lines of a request: the one place the service writes about what it analysed."""

import logging
import time
from collections import Counter
from collections.abc import Generator
from contextlib import contextmanager
from dataclasses import dataclass
from typing import Final

from language_service.logging_.analysis_summary import AnalysisSummary
from language_service.logging_.analysis_watch import AnalysisWatch
from language_service.logging_.fixed_field_formatter import FIELDS_ATTRIBUTE
from language_service.logging_.log_field import LogField
from language_service.logging_.outcome import Outcome
from language_service.logging_.step import Step

ANALYZED: Final = "analyzed"
ANALYSIS_FAILED: Final = "analysis_failed"
NANOSECONDS_PER_MILLISECOND: Final = 1_000_000


@dataclass(frozen=True)
class RequestLog:
    """Writes the success line or the failure line of a request, each with its fixed fields."""

    logger: logging.Logger

    @contextmanager
    def watch(self, reference: str, chars: int) -> Generator[AnalysisWatch]:
        """Times the request; on leaving, writes the success line if answered, else the failure."""
        watch = AnalysisWatch()
        started = time.monotonic_ns()
        try:
            yield watch
        finally:
            elapsed_ms = (time.monotonic_ns() - started) // NANOSECONDS_PER_MILLISECOND
            self._close(watch, reference, chars, elapsed_ms)

    def _close(self, watch: AnalysisWatch, reference: str, chars: int, elapsed_ms: int) -> None:
        if watch.response is None:
            self._failed(reference, watch.step)
            return
        kinds = Counter(occurrence.kind for occurrence in watch.response.occurrences)
        self._analyzed(
            AnalysisSummary(reference, chars, elapsed_ms, watch.response.model_version, kinds)
        )

    def _analyzed(self, summary: AnalysisSummary) -> None:
        fields = {
            LogField.REFERENCE: summary.reference,
            LogField.CHARS: summary.chars,
            LogField.ELAPSED_MS: summary.elapsed_ms,
            LogField.MODEL_VERSION: summary.model_version,
            LogField.COUNT_BY_KIND: dict(summary.count_by_kind),
            LogField.OUTCOME: Outcome.OK,
        }
        self.logger.info(ANALYZED, extra={FIELDS_ATTRIBUTE: fields})

    def _failed(self, reference: str, step: Step) -> None:
        fields = {
            LogField.REFERENCE: reference,
            LogField.STEP: step,
            LogField.OUTCOME: Outcome.FAILED,
        }
        self.logger.error(ANALYSIS_FAILED, extra={FIELDS_ATTRIBUTE: fields})
