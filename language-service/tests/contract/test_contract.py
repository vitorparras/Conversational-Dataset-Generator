"""Decision 0018: the strict contract, with no confidence and no unknown field."""

import pytest

from conftest import Harness
from language_service.contract import AnalyzeResponse, Occurrence

GOOD = {"text": "texto", "language": "pt", "reference": "ref-1", "contract_version": "1"}


def test_extra_field_in_the_request_is_refused(harness: Harness) -> None:
    reply = harness.client.post("/analyze", json={**GOOD, "confidence": 0.5})

    assert reply.status_code == 400
    assert reply.json()["error"] == "invalid_request"
    assert "reference" in reply.json()


@pytest.mark.parametrize("missing", sorted(GOOD))
def test_each_request_field_is_required(harness: Harness, missing: str) -> None:
    body = {key: value for key, value in GOOD.items() if key != missing}

    assert harness.client.post("/analyze", json=body).status_code == 400


def test_no_coercion_a_number_is_not_a_text(harness: Harness) -> None:
    assert harness.client.post("/analyze", json={**GOOD, "text": 5}).status_code == 400


def test_unknown_contract_version_is_a_400_with_the_reference(harness: Harness) -> None:
    reply = harness.client.post("/analyze", json={**GOOD, "contract_version": "99"})

    assert reply.status_code == 400
    assert reply.json() == {"error": "unsupported_contract_version", "reference": "ref-1", "supported": "1"}


def test_reference_that_is_not_opaque_is_refused(harness: Harness) -> None:
    assert harness.client.post("/analyze", json={**GOOD, "reference": "with spaces"}).status_code == 400


def test_invalid_body_does_not_echo_the_input(harness: Harness) -> None:
    reply = harness.client.post("/analyze", content=b'{"text": "ZQX-MARKER-7731", broken')

    assert reply.status_code == 400
    assert "ZQX-MARKER-7731" not in reply.text


def test_models_forbid_extra_fields() -> None:
    assert AnalyzeResponse.model_config["extra"] == "forbid"
    assert Occurrence.model_config["extra"] == "forbid"
    with pytest.raises(ValueError, match="confidence"):
        Occurrence.model_validate(
            {"start": 0, "end": 1, "kind": "k", "part_of_speech": "p", "syntactic_role": "r", "confidence": 1}
        )


def test_occurrence_has_exactly_the_fields_of_decision_0018() -> None:
    assert set(Occurrence.model_fields) == {"start", "end", "kind", "part_of_speech", "syntactic_role"}
    assert set(AnalyzeResponse.model_fields) == {"occurrences", "model_version"}
