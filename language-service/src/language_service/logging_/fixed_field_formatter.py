"""The formatter that writes one JSON line: the event and the allowed fields, nothing else."""

import json
import logging
from typing import Final, override

from language_service.logging_.log_field import LogField
from language_service.logging_.unknown_log_field_error import UnknownLogFieldError

FIELDS_ATTRIBUTE: Final = "fields"
ALLOWED: Final = frozenset(field.value for field in LogField)


class FixedFieldFormatter(logging.Formatter):
    """Renders the message template as the event, never its arguments, and never a traceback.

    A key outside ``LogField`` raises, so a new field is a decision and not a slip.
    """

    @override
    def format(self, record: logging.LogRecord) -> str:
        fields: dict[str, object] = getattr(record, FIELDS_ATTRIBUTE, {})
        unknown = sorted(set(fields) - ALLOWED)
        if unknown:
            raise UnknownLogFieldError(f"log fields outside the allowed list: {unknown}")
        line = {"level": record.levelname, "event": str(record.msg), **fields}
        return json.dumps(line, ensure_ascii=True)
