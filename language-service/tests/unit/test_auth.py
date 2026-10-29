"""The secret gate: no secret or a wrong one is refused, and the refusal carries no request body."""

import asyncio
import logging
from http import HTTPStatus

import pytest
from fastapi.testclient import TestClient

from language_service.auth import SecretVerifier
from language_service.gate import REFUSAL_MESSAGE, SecretGate
from language_service.proof import SecretProver
from support.asgi import Exchange
from support.synthetic import MARKED_BODY, MARKER, SECRET, SECRET_HEADER

PROVER = SecretProver(SECRET)


def test_verifier_accepts_the_startup_secret() -> None:
    verifier = SecretVerifier.from_secret(SECRET)

    assert verifier.accepts(SECRET)


def test_verifier_rejects_an_absent_candidate() -> None:
    verifier = SecretVerifier.from_secret(SECRET)

    assert not verifier.accepts(None)


def test_verifier_rejects_a_candidate_that_differs_in_one_byte() -> None:
    verifier = SecretVerifier.from_secret(SECRET)

    assert not verifier.accepts(SECRET[:-1] + b"X")


def test_request_without_secret_is_refused_with_401(client: TestClient) -> None:
    response = client.post("/health", json=MARKED_BODY)

    assert response.status_code == HTTPStatus.UNAUTHORIZED


def test_request_with_wrong_secret_is_refused_with_401(client: TestClient) -> None:
    response = client.get("/health", headers={"x-service-secret": "wrong"})

    assert response.status_code == HTTPStatus.UNAUTHORIZED


def test_refusal_response_has_no_request_body(client: TestClient) -> None:
    response = client.post("/health", json=MARKED_BODY)

    assert MARKER not in response.text


def test_refusal_is_logged_as_exactly_one_fixed_line(
    client: TestClient, caplog: pytest.LogCaptureFixture
) -> None:
    caplog.set_level(logging.WARNING, logger="language_service")

    client.post("/health", json=MARKED_BODY)

    assert [record.getMessage() for record in caplog.records] == [REFUSAL_MESSAGE]


def test_refusal_log_has_no_request_body(
    client: TestClient, caplog: pytest.LogCaptureFixture
) -> None:
    caplog.set_level(logging.WARNING, logger="language_service")

    client.post("/health", json=MARKED_BODY)

    assert MARKER not in caplog.text


def test_request_with_the_secret_passes_the_gate(client: TestClient) -> None:
    response = client.get("/health", headers=SECRET_HEADER)

    assert response.status_code == HTTPStatus.OK


def test_gate_refuses_without_reading_the_body() -> None:
    exchange = Exchange()
    gate = SecretGate(exchange.application, SecretVerifier.from_secret(SECRET), PROVER)

    asyncio.run(gate(Exchange.scope(), exchange.receive, exchange.send))

    assert (exchange.status, exchange.body_reads) == (HTTPStatus.UNAUTHORIZED, 0)


def test_gate_refuses_before_the_application_is_reached() -> None:
    exchange = Exchange()
    gate = SecretGate(exchange.application, SecretVerifier.from_secret(SECRET), PROVER)

    asyncio.run(gate(Exchange.scope(), exchange.receive, exchange.send))

    assert exchange.application_calls == 0


def test_gate_hands_a_request_with_the_secret_to_the_application() -> None:
    exchange = Exchange()
    gate = SecretGate(exchange.application, SecretVerifier.from_secret(SECRET), PROVER)

    asyncio.run(
        gate(Exchange.scope(((b"x-service-secret", SECRET),)), exchange.receive, exchange.send)
    )

    assert exchange.application_calls == 1
