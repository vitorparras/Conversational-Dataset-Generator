"""The contract models: strict, closed to unknown fields, immutable, without a confidence field."""

import dataclasses

import pytest
from pydantic import ValidationError

from language_service.contract import (
    AnalyzeRequest,
    AnalyzeResponse,
    HealthResponse,
    VersionResponse,
)
from language_service.recognizers.empty import EmptyRecognizer
from support.recognizers import OCCURRENCE, OCCURRENCE_JSON
from support.synthetic import ANALYZE_BODY, MARKER

VERSION = {"service_version": "0.1.0", "contract_version": "1", "model_version": None}
RESPONSE_WITH_A_TEXT_OFFSET = (
    '{"occurrences": [{"start": "0", "end": 3, "kind": "PERSON", '
    '"part_of_speech": "PROPN", "syntactic_role": "obj"}], "model_version": null}'
)


def test_request_accepts_the_four_fields_of_the_contract() -> None:
    request = AnalyzeRequest(**ANALYZE_BODY)

    assert request.text == MARKER


def test_request_refuses_an_unknown_field() -> None:
    with pytest.raises(ValidationError):
        AnalyzeRequest.model_validate({**ANALYZE_BODY, "unknown": "x"})


def test_request_refuses_a_value_the_lax_mode_would_coerce() -> None:
    with pytest.raises(ValidationError):
        AnalyzeRequest.model_validate({**ANALYZE_BODY, "text": MARKER.encode("ascii")})


def test_request_is_immutable() -> None:
    request = AnalyzeRequest(**ANALYZE_BODY)

    with pytest.raises(ValidationError):
        request.text = "changed"


def test_response_refuses_a_confidence_field() -> None:
    with pytest.raises(ValidationError):
        AnalyzeResponse.model_validate(
            {"occurrences": (), "model_version": None, "confidence": 1.0}
        )


def test_response_refuses_an_offset_the_lax_mode_would_coerce() -> None:
    with pytest.raises(ValidationError):
        AnalyzeResponse.model_validate_json(RESPONSE_WITH_A_TEXT_OFFSET)


def test_response_accepts_the_same_json_with_a_numeric_offset() -> None:
    response = AnalyzeResponse.model_validate_json(RESPONSE_WITH_A_TEXT_OFFSET.replace('"0"', "0"))

    assert response.occurrences == (OCCURRENCE,)


def test_response_carries_an_occurrence_with_its_five_fields() -> None:
    response = AnalyzeResponse(occurrences=(OCCURRENCE,), model_version="m1")

    assert response.model_dump(mode="json")["occurrences"] == [OCCURRENCE_JSON]


def test_response_is_immutable() -> None:
    response = AnalyzeResponse(occurrences=(), model_version=None)

    with pytest.raises(ValidationError):
        response.model_version = "changed"


def test_occurrence_is_immutable() -> None:
    with pytest.raises(dataclasses.FrozenInstanceError):
        OCCURRENCE.__setattr__("start", 1)


def test_empty_recognizer_finds_nothing() -> None:
    recognizer = EmptyRecognizer()

    assert tuple(recognizer.recognize(MARKER, "pt")) == ()


def test_empty_recognizer_has_no_model() -> None:
    assert EmptyRecognizer().model_version is None


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
