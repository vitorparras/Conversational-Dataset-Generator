"""The contract fixtures the core reads: the same JSON round-trips here and is refused here."""

import json

import pytest
from pydantic import ValidationError

from language_service.contract import (
    CONTRACT_VERSION,
    AnalyzeItemRequest,
    AnalyzeItemResponse,
    AnalyzeRequest,
    AnalyzeResponse,
)
from support.contract_fixtures import fixture_text

REQUEST = "accepted/request.json"
ASTRAL_RESPONSE = "accepted/response-astral.json"


def test_request_fixture_is_written_back_unchanged() -> None:
    text = fixture_text(REQUEST)

    written = AnalyzeRequest.model_validate_json(text).model_dump_json()

    assert json.loads(written) == json.loads(text)


@pytest.mark.parametrize("fixture", ["accepted/response-empty.json", ASTRAL_RESPONSE])
def test_response_fixture_is_written_back_unchanged(fixture: str) -> None:
    text = fixture_text(fixture)

    written = AnalyzeResponse.model_validate_json(text).model_dump_json()

    assert json.loads(written) == json.loads(text)


def test_request_fixture_speaks_the_version_of_the_service() -> None:
    request = AnalyzeRequest.model_validate_json(fixture_text(REQUEST))

    assert request.contract_version == CONTRACT_VERSION


@pytest.mark.parametrize(("index", "marker"), [(0, "PESSOA_1"), (1, "LUGAR_1")])
def test_offsets_count_code_points(index: int, marker: str) -> None:
    request = AnalyzeRequest.model_validate_json(fixture_text(REQUEST))
    response = AnalyzeResponse.model_validate_json(fixture_text(ASTRAL_RESPONSE))
    occurrence = response.occurrences[index]

    span = request.text[occurrence.start : occurrence.end]

    assert span == marker


def test_request_with_a_field_that_locates_the_material_is_refused() -> None:
    text = fixture_text("refused/request-with-item-id.json")

    with pytest.raises(ValidationError, match="item_id"):
        AnalyzeRequest.model_validate_json(text)


@pytest.mark.parametrize(
    ("fixture", "field"),
    [
        ("refused/response-with-confidence.json", "confidence"),
        ("refused/response-without-syntactic-role.json", "syntactic_role"),
        ("refused/response-with-null-kind.json", "kind"),
        ("refused/response-with-text-offset.json", "start"),
        ("refused/response-without-model-version.json", "model_version"),
    ],
)
def test_response_outside_the_contract_is_refused(fixture: str, field: str) -> None:
    text = fixture_text(fixture)

    with pytest.raises(ValidationError, match=field):
        AnalyzeResponse.model_validate_json(text)


def test_field_name_in_another_case_is_refused() -> None:
    text = fixture_text("refused/request-with-uppercase-field.json")

    with pytest.raises(ValidationError, match="TEXT"):
        AnalyzeRequest.model_validate_json(text)


@pytest.mark.parametrize(
    ("fixture", "model"),
    [
        ("accepted/item-request.json", AnalyzeItemRequest),
        ("accepted/item-response.json", AnalyzeItemResponse),
    ],
)
def test_item_fixture_is_written_back_unchanged(
    fixture: str, model: type[AnalyzeItemRequest] | type[AnalyzeItemResponse]
) -> None:
    text = fixture_text(fixture)

    written = model.model_validate_json(text).model_dump_json()

    assert json.loads(written) == json.loads(text)


def test_item_offsets_count_code_points_in_their_own_text() -> None:
    request = AnalyzeItemRequest.model_validate_json(fixture_text("accepted/item-request.json"))
    response = AnalyzeItemResponse.model_validate_json(fixture_text("accepted/item-response.json"))

    spans = [request.texts[o.text_index].text[o.start : o.end] for o in response.occurrences]

    assert spans == ["PESSOA_1", "ela", "PESSOA_2"]


def test_item_response_without_groups_is_refused() -> None:
    text = fixture_text("refused/item-response-without-groups.json")

    with pytest.raises(ValidationError, match="author_groups"):
        AnalyzeItemResponse.model_validate_json(text)
