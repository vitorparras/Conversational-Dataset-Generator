"""What the real standard streams hold after an analysis, in the success and the failure path."""

import json
import logging
import sys
from collections.abc import Iterator

import pytest

from language_service.contract import AnalyzeRequest
from language_service.logging_ import RequestLog, install_root_handler
from language_service.recognizers.base import Recognizer
from language_service.recognizers.occurrence import Occurrence
from language_service.routes import Routes
from support.recognizers import (
    FixedRecognizer,
    RaisingRecognizer,
    RaisingVersionRecognizer,
    SlowRecognizer,
)
from support.synthetic import MARKER

TEXT = f"before {MARKER} after"
BODY = {"text": TEXT, "language": "pt", "reference": "ref-9", "contract_version": "1"}
OFFSETS = (41917, 41933)
FOUND = (Occurrence(OFFSETS[0], OFFSETS[1], "PERSON", "PROPN", "obj"),)
SLOW_SECONDS = 0.1
SLOW_FLOOR_MS = 90


@pytest.fixture
def to_stderr(capfd: pytest.CaptureFixture[str]) -> Iterator[pytest.CaptureFixture[str]]:
    """The captured streams; the root log is restored after the test."""
    root = logging.getLogger()
    saved, level = list(root.handlers), root.level
    yield capfd
    root.handlers[:] = saved
    root.setLevel(level)
    logging.captureWarnings(False)


def analyse(recognizer: Recognizer) -> None:
    install_root_handler(sys.stderr)
    routes = Routes(recognizer, RequestLog(logging.getLogger("language_service")))
    routes.analyze(AnalyzeRequest(**BODY))


def analyse_failing(recognizer: Recognizer) -> None:
    with pytest.raises(ValueError):
        analyse(recognizer)


def events(captured: pytest.CaptureFixture[str]) -> dict[str, dict[str, object]]:
    lines = [json.loads(line) for line in captured.readouterr().err.splitlines()]
    return {line["event"]: line for line in lines}


def test_streams_after_a_found_occurrence_hold_no_text_and_no_offset(
    to_stderr: pytest.CaptureFixture[str],
) -> None:
    analyse(FixedRecognizer(FOUND, "m1"))

    captured = to_stderr.readouterr()
    tokens = (MARKER, str(OFFSETS[0]), str(OFFSETS[1]))

    assert [token in captured.out + captured.err for token in tokens] == [False] * 3


def test_success_line_counts_the_occurrences_by_kind(
    to_stderr: pytest.CaptureFixture[str],
) -> None:
    analyse(FixedRecognizer(FOUND, "m1"))

    line = events(to_stderr)["analyzed"]

    assert (line["count_by_kind"], line["model_version"], line["outcome"], line["chars"]) == (
        {"PERSON": 1},
        "m1",
        "ok",
        len(TEXT),
    )


def test_success_writes_no_failure_line(to_stderr: pytest.CaptureFixture[str]) -> None:
    analyse(FixedRecognizer(FOUND, "m1"))

    assert "analysis_failed" not in events(to_stderr)


def test_success_line_reports_at_least_the_time_the_analysis_took(
    to_stderr: pytest.CaptureFixture[str],
) -> None:
    analyse(SlowRecognizer(SLOW_SECONDS))

    line = events(to_stderr)["analyzed"]

    assert isinstance(line["elapsed_ms"], int)
    assert line["elapsed_ms"] >= SLOW_FLOOR_MS


def test_failure_in_recognition_names_reference_and_step_and_leaves_the_streams_clean(
    to_stderr: pytest.CaptureFixture[str],
) -> None:
    analyse_failing(RaisingRecognizer())

    captured = to_stderr.readouterr()
    line = json.loads(captured.err.splitlines()[0])

    assert (line["reference"], line["step"], MARKER in captured.out + captured.err) == (
        "ref-9",
        "recognize",
        False,
    )


def test_failure_while_building_the_response_names_the_respond_step(
    to_stderr: pytest.CaptureFixture[str],
) -> None:
    analyse_failing(RaisingVersionRecognizer())

    captured = to_stderr.readouterr()
    line = json.loads(captured.err.splitlines()[0])

    assert (line["step"], MARKER in captured.out + captured.err) == ("respond", False)
