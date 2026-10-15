"""The state routes answer the minimal contract, and nothing else is served."""

from http import HTTPStatus

from fastapi.testclient import TestClient

from support.synthetic import SECRET_HEADER


def test_health_answers_ok(client: TestClient) -> None:
    response = client.get("/health", headers=SECRET_HEADER)

    assert response.json() == {"status": "ok"}


def test_version_reports_service_and_contract(client: TestClient) -> None:
    response = client.get("/version", headers=SECRET_HEADER)

    assert response.json() == {"service_version": "0.1.0", "contract_version": "1"}


def test_no_documentation_route_is_served(client: TestClient) -> None:
    response = client.get("/docs", headers=SECRET_HEADER)

    assert response.status_code == HTTPStatus.NOT_FOUND
