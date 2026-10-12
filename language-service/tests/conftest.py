"""Shared fixtures: the application built in process, with its log kept in memory."""

import io
from collections.abc import Callable

import pytest
from fastapi import FastAPI
from fastapi.testclient import TestClient

from language_service.app import create_app
from language_service.auth import SecretVerifier
from language_service.logging_ import ServiceLog
from language_service.recognizers.base import Recognizer
from language_service.recognizers.empty import EmptyRecognizer

# The one policy for properties: profile, seed and its report (see support/property_policy.py).
pytest_plugins = ["support.property_policy"]

SECRET = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
SECRET_HEADER = "X-Service-Secret"
MARKER = "ZQX-MARKER-7731"


class Harness:
    """An application, its in-memory log sink and a client that sends the secret."""

    def __init__(self, recognizer: Recognizer | None = None) -> None:
        self.sink = io.StringIO()
        self.app: FastAPI = create_app(
            verifier=SecretVerifier(SECRET.encode("ascii")),
            recognizer=recognizer if recognizer is not None else EmptyRecognizer(),
            log=ServiceLog([self.sink]),
        )
        self.client = TestClient(self.app, headers={SECRET_HEADER: SECRET}, raise_server_exceptions=False)
        self.anonymous = TestClient(self.app, raise_server_exceptions=False)

    @property
    def log_lines(self) -> list[str]:
        """What the application wrote to its log so far, one string per line."""
        return [line for line in self.sink.getvalue().splitlines() if line]


@pytest.fixture
def make_harness() -> Callable[..., Harness]:
    """Build a harness, optionally around another recognizer."""
    return Harness


@pytest.fixture
def harness() -> Harness:
    """A harness around the empty recognizer."""
    return Harness()
