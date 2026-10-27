"""The log of the process has one destination: the one place to sweep for text is stderr."""

import io
import logging
import sys
from collections.abc import Generator, Iterator
from contextlib import contextmanager
from typing import TextIO

import pytest
from fastapi.testclient import TestClient

from language_service.logging_ import FixedFieldHandler, install_root_handler
from language_service.server import build_config
from support.log_destinations import handlers_by_logger, stray_destinations
from support.synthetic import ANALYZE_BODY, SECRET_HEADER

PORT = 8123


@pytest.fixture
def process_log(client: TestClient) -> Iterator[TextIO]:
    """The log as the entry point leaves it, after a health and an analysis were answered."""
    root = logging.getLogger()
    saved, level = list(root.handlers), root.level
    try:
        install_root_handler(sys.stderr)
        build_config(client.app, PORT)
        client.get("/health", headers=SECRET_HEADER)
        client.post("/analyze", json=ANALYZE_BODY, headers=SECRET_HEADER)
        yield sys.stderr
    finally:
        root.handlers[:] = saved
        root.setLevel(level)
        logging.captureWarnings(False)


@contextmanager
def attached(logger_name: str, handler: logging.Handler) -> Generator[None]:
    logger = logging.getLogger(logger_name)
    logger.addHandler(handler)
    try:
        yield
    finally:
        logger.removeHandler(handler)


def test_the_process_log_has_one_handler_and_it_is_the_fixed_field_handler(
    process_log: TextIO,
) -> None:
    assert [(name, type(handler)) for name, handler in handlers_by_logger()] == [
        ("root", FixedFieldHandler)
    ]


def test_after_answering_no_handler_besides_the_fixed_one_exists(process_log: TextIO) -> None:
    assert stray_destinations(process_log) == ()


@pytest.mark.parametrize("logger_name", ["language_service.zqx_extra", "uvicorn.error", "root"])
def test_a_handler_beyond_the_fixed_one_is_reported_with_the_name_of_its_logger(
    process_log: TextIO, logger_name: str
) -> None:
    with attached(logger_name, logging.StreamHandler(io.StringIO())):
        strays = stray_destinations(process_log)

    assert strays == (f"{logger_name}: StreamHandler",)


@pytest.mark.parametrize("logger_name", ["mutmut", "py.warnings"])
def test_a_null_handler_discards_what_it_receives_and_is_no_destination(
    process_log: TextIO, logger_name: str
) -> None:
    with attached(logger_name, logging.NullHandler()):
        strays = stray_destinations(process_log)
        handlers = [(name, type(handler)) for name, handler in handlers_by_logger()]

    assert strays == ()
    assert handlers == [("root", FixedFieldHandler)]


def test_a_fixed_field_handler_on_another_stream_is_reported(process_log: TextIO) -> None:
    with attached("root", FixedFieldHandler(io.StringIO())):
        strays = stray_destinations(process_log)

    assert strays == ("root: FixedFieldHandler",)
