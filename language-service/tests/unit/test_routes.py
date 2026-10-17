"""The routes answer the contract over the recognizer they were given; nothing else is served."""

from http import HTTPStatus

import pytest
from fastapi.testclient import TestClient

from language_service.contract import AnalyzeRequest
from language_service.routes import Routes
from support.recognizers import OCCURRENCE, FixedRecognizer
from support.synthetic import ANALYZE_BODY, SECRET_HEADER

ROUTES = Routes(FixedRecognizer(found=(OCCURRENCE,), model_version="m1"))


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


def test_analyze_carries_what_the_recognizer_found() -> None:
    response = ROUTES.analyze(AnalyzeRequest(**ANALYZE_BODY))

    assert response.occurrences == (OCCURRENCE,)


def test_analyze_carries_the_recognizer_model_version() -> None:
    response = ROUTES.analyze(AnalyzeRequest(**ANALYZE_BODY))

    assert response.model_version == "m1"


def test_version_carries_the_recognizer_model_version() -> None:
    assert ROUTES.version().model_version == "m1"
