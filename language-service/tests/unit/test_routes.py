"""The three routes, and the absence of any other."""

from conftest import Harness


def test_health_answers_ok_with_an_explicit_null_model(harness: Harness) -> None:
    reply = harness.client.get("/health")

    assert reply.status_code == 200
    assert reply.json() == {"status": "ok", "model_version": None}


def test_version_reports_service_contract_and_an_explicit_null_model(harness: Harness) -> None:
    body = harness.client.get("/version").json()

    assert body == {"service_version": "0.1.0", "contract_version": "1", "model_version": None}


def test_analyze_with_the_empty_recognizer_finds_nothing(harness: Harness) -> None:
    reply = harness.client.post(
        "/analyze",
        json={"text": "qualquer texto", "language": "pt", "reference": "ref-1", "contract_version": "1"},
    )

    assert reply.status_code == 200
    assert reply.json() == {"occurrences": [], "model_version": None}


def test_the_application_has_exactly_the_three_routes(harness: Harness) -> None:
    paths = {route.path for route in harness.app.routes}  # type: ignore[attr-defined]

    assert paths == {"/health", "/version", "/analyze"}


def test_generated_documentation_does_not_exist(harness: Harness) -> None:
    for path in ("/docs", "/redoc", "/openapi.json"):
        assert harness.client.get(path).status_code == 404
