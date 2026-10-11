"""Issue 29 CA-2: no secret, no service; and the refusal is logged without the body."""

import hmac
import json
from unittest import mock

import pytest

from conftest import MARKER, SECRET, SECRET_HEADER, Harness
from language_service.auth import SecretVerifier

BODY = {"text": MARKER, "language": "pt", "reference": "ref-1", "contract_version": "1"}


@pytest.mark.parametrize("route", ["/health", "/version"])
def test_request_without_the_secret_is_refused(harness: Harness, route: str) -> None:
    reply = harness.anonymous.get(route)

    assert reply.status_code == 401


def test_analyze_without_the_secret_is_refused_and_the_log_has_no_body(harness: Harness) -> None:
    reply = harness.anonymous.post("/analyze", json=BODY)

    assert reply.status_code == 401
    assert MARKER not in reply.text
    (line,) = harness.log_lines
    event = json.loads(line)
    assert event["step"] == "auth"
    assert event["outcome"] == "refused"
    assert MARKER not in line


def test_wrong_secret_is_refused_like_a_missing_one(harness: Harness) -> None:
    reply = harness.anonymous.get("/health", headers={SECRET_HEADER: SECRET[:-1] + "0"})

    assert reply.status_code == 401


def test_unknown_route_is_refused_before_routing(harness: Harness) -> None:
    # Otherwise a 404 would tell a caller without the secret which routes exist.
    assert harness.anonymous.get("/docs").status_code == 401
    assert harness.anonymous.get("/openapi.json").status_code == 401


def test_secret_comparison_goes_through_compare_digest() -> None:
    verifier = SecretVerifier(b"a-secret-of-some-length")
    with mock.patch.object(hmac, "compare_digest", wraps=hmac.compare_digest) as spy:
        assert verifier.accepts(b"a-secret-of-some-length")
        assert not verifier.accepts(b"another")
        assert not verifier.accepts(None)
    assert spy.call_count == 2


def test_verifier_does_not_keep_the_raw_secret() -> None:
    secret = b"a-secret-of-some-length"
    verifier = SecretVerifier(secret)

    assert secret not in repr(vars(verifier)).encode()
    assert all(secret != value for value in vars(verifier).values())


def test_empty_secret_is_not_a_secret() -> None:
    with pytest.raises(ValueError, match="empty"):
        SecretVerifier(b"")
