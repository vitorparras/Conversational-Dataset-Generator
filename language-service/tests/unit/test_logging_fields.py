"""The log refuses a key outside the fixed list and renders nothing a third party wrote."""

import io
import logging
import warnings
from collections.abc import Iterator

import pytest

from language_service.logging_ import FixedFieldHandler, UnknownLogFieldError, install_root_handler
from language_service.logging_.fixed_field_formatter import FIELDS_ATTRIBUTE
from support.synthetic import MARKER

OWN = "language_service.test_fields"
FOREIGN = "third_party.library"


def logger_over(stream: io.StringIO, name: str) -> logging.Logger:
    logger = logging.getLogger(name)
    logger.handlers[:] = [FixedFieldHandler(stream)]
    logger.propagate = False
    logger.setLevel(logging.INFO)
    return logger


@pytest.fixture(autouse=True)
def release_the_named_loggers() -> Iterator[None]:
    """The two loggers a test gave a handler go back to having none, whatever ran after."""
    yield
    for name in (OWN, FOREIGN):
        logger = logging.getLogger(name)
        logger.handlers.clear()
        logger.propagate = True
        logger.setLevel(logging.NOTSET)


@pytest.fixture
def root_stream() -> Iterator[io.StringIO]:
    """The root log sent to a buffer, restored with the warnings hook after the test."""
    root = logging.getLogger()
    saved, level = list(root.handlers), root.level
    stream = io.StringIO()
    install_root_handler(stream)
    yield stream
    root.handlers[:] = saved
    root.setLevel(level)
    logging.captureWarnings(False)


def test_key_outside_the_list_is_an_error_not_silence() -> None:
    logger = logger_over(io.StringIO(), OWN)

    with pytest.raises(UnknownLogFieldError, match="text"):
        logger.info("event", extra={FIELDS_ATTRIBUTE: {"text": MARKER}})


def test_key_outside_the_list_writes_nothing() -> None:
    stream = io.StringIO()
    logger = logger_over(stream, OWN)

    with pytest.raises(UnknownLogFieldError):
        logger.info("event", extra={FIELDS_ATTRIBUTE: {"offsets": [1, 2]}})

    assert stream.getvalue() == ""


def test_message_arguments_are_not_rendered() -> None:
    stream = io.StringIO()

    logger_over(stream, OWN).info("seen %s", MARKER)

    assert MARKER not in stream.getvalue()


def test_exception_text_and_traceback_are_not_rendered() -> None:
    stream = io.StringIO()
    logger = logger_over(stream, OWN)

    with pytest.raises(ValueError) as caught:
        raise ValueError(MARKER)
    logger.error("failed", exc_info=caught.value)

    assert MARKER not in stream.getvalue()


def test_message_of_a_third_party_logger_is_not_rendered() -> None:
    stream = io.StringIO()

    logger_over(stream, FOREIGN).info(f"already formatted with {MARKER}")

    assert MARKER not in stream.getvalue()


def test_third_party_record_is_named_by_its_logger() -> None:
    stream = io.StringIO()

    logger_over(stream, FOREIGN).info(f"already formatted with {MARKER}")

    assert FOREIGN in stream.getvalue()


def test_own_message_is_rendered_as_the_event() -> None:
    stream = io.StringIO()

    logger_over(stream, OWN).info("constant_event")

    assert "constant_event" in stream.getvalue()


def test_warning_text_does_not_reach_the_log(root_stream: io.StringIO) -> None:
    with warnings.catch_warnings():
        warnings.simplefilter("always")
        warnings.warn(f"model said {MARKER}", UserWarning, stacklevel=1)

    assert (MARKER in root_stream.getvalue(), "py.warnings" in root_stream.getvalue()) == (
        False,
        True,
    )
