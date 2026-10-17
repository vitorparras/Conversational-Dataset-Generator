"""The response of the version route."""

from language_service.contract.message import Message


class VersionResponse(Message):
    """Which service, which contract, which model; no model is an explicit null."""

    service_version: str
    contract_version: str
    model_version: str | None
