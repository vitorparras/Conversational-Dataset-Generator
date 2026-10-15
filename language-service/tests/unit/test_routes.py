"""The state routes answer the minimal contract, and nothing else is served."""

from http import HTTPStatus

import pytest
from fastapi.testclient import TestClient

from support.synthetic import SECRET_HEADER


def test_health_answers_ok(client: TestClient) -> None:
    response = client.get("/health", headers=SECRET_HEADER)

    assert response.json() == {"status": "ok"}


def test_version_reports_service_contract_and_model(client: TestClient) -> None:
    response = client.get("/version", headers=SECRET_HEADER)

    assert response.json() == {
        "service_version": "0.1.0",
        "contract_version": "1",
        "model_version": None,
    }


@pytest.mark.parametrize("path", ["/docs", "/redoc", "/openapi.json"])
def test_no_documentation_route_is_served(client: TestClient, path: str) -> None:
    response = client.get(path, headers=SECRET_HEADER)

    assert response.status_code == HTTPStatus.NOT_FOUND
