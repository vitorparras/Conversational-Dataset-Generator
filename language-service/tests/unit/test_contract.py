"""The contract models are closed to unknown fields and take no coerced value."""

import pytest
from pydantic import ValidationError

from language_service.contract import HealthResponse, VersionResponse

VERSION = {"service_version": "0.1.0", "contract_version": "1"}


def test_health_response_refuses_an_unknown_field() -> None:
    with pytest.raises(ValidationError):
        HealthResponse.model_validate({"status": "ok", "unknown": "x"})


def test_version_response_refuses_an_unknown_field() -> None:
    with pytest.raises(ValidationError):
        VersionResponse.model_validate({**VERSION, "unknown": "x"})


def test_version_response_refuses_a_value_the_lax_mode_would_coerce() -> None:
    with pytest.raises(ValidationError):
        VersionResponse.model_validate({**VERSION, "service_version": b"0.1.0"})


def test_health_response_accepts_the_contract_shape() -> None:
    assert HealthResponse.model_validate({"status": "ok"}).status == "ok"
