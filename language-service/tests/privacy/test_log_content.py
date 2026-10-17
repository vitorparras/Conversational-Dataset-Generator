"""What the log says about an analysis, in the success path and in the failure path."""

import io
import json
import logging
from collections.abc import Iterator

import pytest
from fastapi.testclient import TestClient

from language_service.app import create_app
from language_service.logging_ import install_root_handler
from language_service.recognizers.occurrence import Occurrence
from support.recognizers import FixedRecognizer, RaisingRecognizer
from support.synthetic import MARKER, SECRET, SECRET_HEADER

TEXT = f"before {MARKER} after"
BODY = {"text": TEXT, "language": "pt", "reference": "ref-9", "contract_version": "1"}
OFFSETS = (41917, 41933)
FOUND = (Occurrence(OFFSETS[0], OFFSETS[1], "PERSON", "PROPN", "obj"),)


@pytest.fixture
def written() -> Iterator[io.StringIO]:
    """The root log redirected to a buffer for the test, then restored."""
    root = logging.getLogger()
    saved, level = list(root.handlers), root.level
    stream = io.StringIO()
    install_root_handler(stream)
    yield stream
    root.handlers[:] = saved
    root.setLevel(level)


def analyse_with(recognizer: FixedRecognizer | RaisingRecognizer) -> int:
    client = TestClient(create_app(SECRET, recognizer), raise_server_exceptions=False)
    return client.post("/analyze", json=BODY, headers=SECRET_HEADER).status_code


def event_of(written: io.StringIO, event: str) -> dict[str, object]:
    lines = [json.loads(line) for line in written.getvalue().splitlines()]
    return next(line for line in lines if line["event"] == event)


def test_log_of_a_found_occurrence_holds_no_text_and_no_offset(written: io.StringIO) -> None:
    analyse_with(FixedRecognizer(FOUND, "m1"))

    log = written.getvalue()

    assert [token in log for token in (MARKER, str(OFFSETS[0]), str(OFFSETS[1]))] == [False] * 3


def test_log_of_a_success_counts_the_occurrences_by_kind(written: io.StringIO) -> None:
    analyse_with(FixedRecognizer(FOUND, "m1"))

    line = event_of(written, "analyzed")

    assert (line["count_by_kind"], line["model_version"], line["outcome"]) == (
        {"PERSON": 1},
        "m1",
        "ok",
    )


def test_log_of_a_failure_names_the_reference_and_the_step_only(written: io.StringIO) -> None:
    status = analyse_with(RaisingRecognizer())

    line = event_of(written, "analysis_failed")

    assert (status, line["reference"], line["step"], MARKER in written.getvalue()) == (
        500,
        "ref-9",
        "recognize",
        False,
    )
