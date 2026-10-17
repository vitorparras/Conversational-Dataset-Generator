"""The handler that writes through the fixed-field formatter and lets a wrong key escape."""

import logging
import sys
from typing import TextIO, override

from language_service.logging_.fixed_field_formatter import FixedFieldFormatter
from language_service.logging_.unknown_log_field_error import UnknownLogFieldError


class FixedFieldHandler(logging.StreamHandler[TextIO]):
    """Writes to ``stream``; the standard handler would swallow a wrong key, this one raises."""

    def __init__(self, stream: TextIO) -> None:
        super().__init__(stream)
        self.setFormatter(FixedFieldFormatter())

    @override
    def handleError(self, record: logging.LogRecord) -> None:
        error = sys.exc_info()[1]
        if isinstance(error, UnknownLogFieldError):
            raise error
        super().handleError(record)
