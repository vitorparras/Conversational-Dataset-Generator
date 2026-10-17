"""The log lines of a request: the one place the service writes about what it analysed."""

import logging
from dataclasses import dataclass
from typing import Final

from language_service.logging_.analysis_summary import AnalysisSummary
from language_service.logging_.fixed_field_formatter import FIELDS_ATTRIBUTE
from language_service.logging_.log_field import LogField
from language_service.logging_.outcome import Outcome
from language_service.logging_.step import Step

ANALYZED: Final = "analyzed"
ANALYSIS_FAILED: Final = "analysis_failed"


@dataclass(frozen=True)
class RequestLog:
    """Writes the success line and the failure line, each with its fixed fields."""

    logger: logging.Logger

    def analyzed(self, summary: AnalysisSummary) -> None:
        fields = {
            LogField.REFERENCE: summary.reference,
            LogField.CHARS: summary.chars,
            LogField.ELAPSED_MS: summary.elapsed_ms,
            LogField.MODEL_VERSION: summary.model_version,
            LogField.COUNT_BY_KIND: dict(summary.count_by_kind),
            LogField.OUTCOME: Outcome.OK,
        }
        self.logger.info(ANALYZED, extra={FIELDS_ATTRIBUTE: fields})

    def failed(self, reference: str, step: Step) -> None:
        fields = {
            LogField.REFERENCE: reference,
            LogField.STEP: step,
            LogField.OUTCOME: Outcome.FAILED,
        }
        self.logger.error(ANALYSIS_FAILED, extra={FIELDS_ATTRIBUTE: fields})
