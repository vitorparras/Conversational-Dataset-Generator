"""The log refuses a key outside the fixed list, and never renders arguments or a traceback."""

import io
import logging

import pytest

from language_service.logging_ import FixedFieldHandler, UnknownLogFieldError
from language_service.logging_.fixed_field_formatter import FIELDS_ATTRIBUTE
from support.synthetic import MARKER


def logger_over(stream: io.StringIO) -> logging.Logger:
    logger = logging.getLogger("test_logging_fields")
    logger.handlers[:] = [FixedFieldHandler(stream)]
    logger.propagate = False
    logger.setLevel(logging.INFO)
    return logger


def test_key_outside_the_list_is_an_error_not_silence() -> None:
    logger = logger_over(io.StringIO())

    with pytest.raises(UnknownLogFieldError, match="text"):
        logger.info("event", extra={FIELDS_ATTRIBUTE: {"text": MARKER}})


def test_key_outside_the_list_writes_nothing() -> None:
    stream = io.StringIO()
    logger = logger_over(stream)

    with pytest.raises(UnknownLogFieldError):
        logger.info("event", extra={FIELDS_ATTRIBUTE: {"offsets": [1, 2]}})

    assert stream.getvalue() == ""


def test_message_arguments_are_not_rendered() -> None:
    stream = io.StringIO()

    logger_over(stream).info("seen %s", MARKER)

    assert MARKER not in stream.getvalue()


def test_exception_text_and_traceback_are_not_rendered() -> None:
    stream = io.StringIO()
    logger = logger_over(stream)

    with pytest.raises(ValueError) as caught:
        raise ValueError(MARKER)
    logger.error("failed", exc_info=caught.value)

    assert MARKER not in stream.getvalue()
