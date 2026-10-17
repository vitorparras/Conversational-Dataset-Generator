"""The formatter that writes one JSON line: the event and the allowed fields, nothing else."""

import json
import logging
from typing import Final, override

from language_service.logging_.log_field import LogField
from language_service.logging_.unknown_log_field_error import UnknownLogFieldError

FIELDS_ATTRIBUTE: Final = "fields"
OWN_LOGGER: Final = "language_service"
ALLOWED: Final = frozenset(field.value for field in LogField)


class FixedFieldFormatter(logging.Formatter):
    """Renders the service's own message template as the event, never arguments or a traceback.

    A record from any other logger is named by its logger only: its message may hold input text.

    A key outside ``LogField`` raises, so a new field is a decision and not a slip.
    """

    @override
    def format(self, record: logging.LogRecord) -> str:
        fields: dict[str, object] = getattr(record, FIELDS_ATTRIBUTE, {})
        unknown = sorted(set(fields) - ALLOWED)
        if unknown:
            raise UnknownLogFieldError(f"log fields outside the allowed list: {unknown}")
        line = {"level": record.levelname, "event": _event_of(record), **fields}
        return json.dumps(line, ensure_ascii=True)


def _event_of(record: logging.LogRecord) -> str:
    if record.name == OWN_LOGGER or record.name.startswith(f"{OWN_LOGGER}."):
        return str(record.msg)
    return record.name
