"""The request of the analysis route."""

from language_service.contract.message import Message


class AnalyzeRequest(Message):
    """What the core sends: the text, its language, an opaque reference and the contract version."""

    text: str
    language: str
    reference: str
    contract_version: str
