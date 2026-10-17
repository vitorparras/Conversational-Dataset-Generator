"""The response of the health route."""

from typing import Literal

from language_service.contract.message import Message


class HealthResponse(Message):
    """The service is up and the secret was accepted."""

    status: Literal["ok"]
