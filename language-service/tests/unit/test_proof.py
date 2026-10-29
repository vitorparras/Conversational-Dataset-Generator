"""The version route proves that the process holds the secret, and asks for none to do it."""

import hashlib
import hmac
from http import HTTPStatus

import pytest
from fastapi.testclient import TestClient

from language_service.proof import SecretProver
from support.synthetic import ANALYZE_BODY, SECRET, SECRET_HEADER

NONCE = "ab" * 32
OTHER_NONCE = "cd" * 32
PROOF = "x-service-proof"


def nonce_of(value: str) -> dict[str, str]:
    return {"x-service-nonce": value}


def test_proof_is_hmac_sha256_of_the_nonce_keyed_by_the_secret() -> None:
    expected = hmac.new(SECRET, NONCE.encode("ascii"), hashlib.sha256).hexdigest()

    assert SecretProver(SECRET).proof(NONCE.encode("ascii")) == expected.encode("ascii")


def test_version_with_a_nonce_answers_the_proof_without_the_secret(client: TestClient) -> None:
    response = client.get("/version", headers=nonce_of(NONCE))

    assert (response.status_code, response.headers[PROOF]) == (
        HTTPStatus.OK,
        hmac.new(SECRET, NONCE.encode("ascii"), hashlib.sha256).hexdigest(),
    )


def test_two_nonces_get_two_different_proofs(client: TestClient) -> None:
    first = client.get("/version", headers=nonce_of(NONCE)).headers[PROOF]
    second = client.get("/version", headers=nonce_of(OTHER_NONCE)).headers[PROOF]

    assert first != second


def test_version_without_a_nonce_answers_no_proof_and_needs_no_secret(client: TestClient) -> None:
    response = client.get("/version")

    assert (response.status_code, PROOF in response.headers) == (HTTPStatus.OK, False)


@pytest.mark.parametrize(
    "nonce",
    ["", "ab", "ab" * 15, "AB" * 32, "zz" * 32, "ab" * 65, "ab " * 20],
    ids=["empty", "short", "just-short", "uppercase", "not-hex", "too-long", "spaces"],
)
def test_version_with_a_malformed_nonce_is_refused_without_proof(
    client: TestClient, nonce: str
) -> None:
    response = client.get("/version", headers=nonce_of(nonce))

    assert (response.status_code, PROOF in response.headers) == (HTTPStatus.BAD_REQUEST, False)


def test_the_proof_never_carries_the_secret(client: TestClient) -> None:
    response = client.get("/version", headers=nonce_of(NONCE))

    assert SECRET.decode("ascii") not in str(response.headers)


@pytest.mark.parametrize("path", ["/health", "/analyze"])
def test_other_routes_still_refuse_a_request_without_the_secret_and_prove_nothing(
    client: TestClient, path: str
) -> None:
    response = client.request(
        "POST" if path == "/analyze" else "GET", path, headers=nonce_of(NONCE), json=ANALYZE_BODY
    )

    assert (response.status_code, PROOF in response.headers) == (HTTPStatus.UNAUTHORIZED, False)


def test_other_routes_with_the_secret_still_pass(client: TestClient) -> None:
    response = client.get("/health", headers={**SECRET_HEADER, **nonce_of(NONCE)})

    assert (response.status_code, PROOF in response.headers) == (HTTPStatus.OK, False)
