"""The contract models: strict, closed to unknown fields, and without a confidence field."""

import pytest
from pydantic import ValidationError

from language_service.contract import (
    AnalyzeRequest,
    AnalyzeResponse,
    HealthResponse,
    VersionResponse,
)
from language_service.recognizers.base import Occurrence
from language_service.recognizers.empty import EmptyRecognizer
from support.synthetic import ANALYZE_BODY, MARKER

OCCURRENCE = Occurrence(start=0, end=3, kind="PERSON", part_of_speech="PROPN", syntactic_role="obj")
VERSION = {"service_version": "0.1.0", "contract_version": "1", "model_version": None}


def test_request_accepts_the_four_fields_of_the_contract() -> None:
    request = AnalyzeRequest(**ANALYZE_BODY)

    assert request.text == MARKER


def test_request_refuses_an_unknown_field() -> None:
    with pytest.raises(ValidationError):
        AnalyzeRequest.model_validate({**ANALYZE_BODY, "unknown": "x"})


def test_request_refuses_a_coerced_type() -> None:
    with pytest.raises(ValidationError):
        AnalyzeRequest.model_validate({**ANALYZE_BODY, "text": 123})


def test_response_refuses_a_confidence_field() -> None:
    with pytest.raises(ValidationError):
        AnalyzeResponse.model_validate(
            {"occurrences": [], "model_version": None, "confidence": 1.0}
        )


def test_response_carries_an_occurrence_with_the_five_fields_of_decision_0018() -> None:
    response = AnalyzeResponse(occurrences=[OCCURRENCE], model_version="m1")

    assert response.model_dump()["occurrences"] == [
        {"start": 0, "end": 3, "kind": "PERSON", "part_of_speech": "PROPN", "syntactic_role": "obj"}
    ]


def test_empty_recognizer_finds_nothing_and_has_no_model() -> None:
    recognizer = EmptyRecognizer()

    found = recognizer.recognize(MARKER, "pt")

    assert (tuple(found), recognizer.model_version) == ((), None)


def test_health_response_refuses_an_unknown_field() -> None:
    with pytest.raises(ValidationError):
        HealthResponse.model_validate({"status": "ok", "unknown": "x"})


def test_version_response_refuses_an_unknown_field() -> None:
    with pytest.raises(ValidationError):
        VersionResponse.model_validate({**VERSION, "unknown": "x"})


def test_version_response_refuses_a_value_the_lax_mode_would_coerce() -> None:
    with pytest.raises(ValidationError):
        VersionResponse.model_validate({**VERSION, "service_version": b"0.1.0"})


def test_health_response_accepts_the_contract_shape() -> None:
    assert HealthResponse.model_validate({"status": "ok"}).status == "ok"
