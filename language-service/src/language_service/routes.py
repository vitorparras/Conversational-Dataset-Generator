"""What each route answers. Registering them is the composition root's job, not this module's."""

from dataclasses import dataclass
from typing import Final

from starlette.requests import Request
from starlette.responses import JSONResponse

from language_service import __version__
from language_service.contract import (
    CONTRACT_VERSION,
    AnalyzeItemRequest,
    AnalyzeItemResponse,
    AnalyzeRequest,
    AnalyzeResponse,
    HealthResponse,
    VersionResponse,
)
from language_service.logging_ import RequestLog, Step
from language_service.recognizers.base import Recognizer
from language_service.recognizers.name_rule import group_by_name

HTTP_BAD_REQUEST: Final = 400


@dataclass(frozen=True)
class Routes:
    """The answers of the service, over one recognizer and the log of its requests."""

    recognizer: Recognizer
    log: RequestLog

    def health(self) -> HealthResponse:
        return HealthResponse(status="ok")

    def version(self) -> VersionResponse:
        return VersionResponse(
            service_version=__version__,
            contract_version=CONTRACT_VERSION,
            model_version=self.recognizer.model_version,
        )

    def analyze(self, body: AnalyzeRequest) -> AnalyzeResponse:
        with self.log.watch(body.reference, len(body.text)) as watch:
            found = tuple(self.recognizer.recognize(body.text, body.language))
            watch.reach(Step.RESPOND)
            return watch.answer(
                AnalyzeResponse(occurrences=found, model_version=self.recognizer.model_version)
            )

    def analyze_item(self, body: AnalyzeItemRequest) -> AnalyzeItemResponse:
        """Each text read alone, as ``analyze`` reads it; the name rule groups the whole item."""
        with self.log.watch(body.reference, sum(len(text.text) for text in body.texts)) as watch:
            found = [tuple(self.recognizer.recognize(t.text, body.language)) for t in body.texts]
            watch.reach(Step.RESPOND)
            return watch.answer(group_by_name(body.texts, found, self.recognizer.model_version))


async def refuse_invalid_request(request: Request, error: Exception) -> JSONResponse:
    """A request the contract rejects gets a bare refusal: the default would echo the input."""
    return JSONResponse({"error": "invalid_request"}, status_code=HTTP_BAD_REQUEST)
