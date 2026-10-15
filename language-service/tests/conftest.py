"""Shared fixture: a client over an application started with the synthetic secret."""

import pytest
from fastapi.testclient import TestClient

from language_service.app import create_app
from support.synthetic import SECRET


@pytest.fixture
def client() -> TestClient:
    return TestClient(create_app(SECRET), raise_server_exceptions=False)
