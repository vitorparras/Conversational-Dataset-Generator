"""``/analyze`` answers "nothing found" in the contract's shape, behind the gate, without echoes."""

from http import HTTPStatus

from fastapi.testclient import TestClient

from support.synthetic import ANALYZE_BODY, MARKER, SECRET_HEADER


def test_analyze_without_secret_is_refused_with_401(client: TestClient) -> None:
    response = client.post("/analyze", json=ANALYZE_BODY)

    assert response.status_code == HTTPStatus.UNAUTHORIZED


def test_analyze_answers_nothing_found(client: TestClient) -> None:
    response = client.post("/analyze", json=ANALYZE_BODY, headers=SECRET_HEADER)

    assert response.status_code == HTTPStatus.OK
    assert response.json() == {"occurrences": [], "model_version": None}


def test_analyze_refuses_an_unknown_field(client: TestClient) -> None:
    response = client.post(
        "/analyze", json={**ANALYZE_BODY, "confidence": 1.0}, headers=SECRET_HEADER
    )

    assert response.status_code == HTTPStatus.BAD_REQUEST


def test_analyze_refuses_a_missing_field_without_echoing_the_request(
    client: TestClient,
) -> None:
    response = client.post("/analyze", json={"reference": MARKER}, headers=SECRET_HEADER)

    assert response.status_code == HTTPStatus.BAD_REQUEST
    assert MARKER not in response.text
