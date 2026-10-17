"""What each route answers. Registering them is the composition root's job, not this module's."""

import time
from collections import Counter
from dataclasses import dataclass
from typing import Final

from starlette.requests import Request
from starlette.responses import JSONResponse

from language_service import __version__
from language_service.contract import (
    CONTRACT_VERSION,
    AnalyzeRequest,
    AnalyzeResponse,
    HealthResponse,
    VersionResponse,
)
from language_service.logging_ import AnalysisSummary, RequestLog, Step
from language_service.recognizers.base import Recognizer

HTTP_BAD_REQUEST: Final = 400
NANOSECONDS_PER_MILLISECOND: Final = 1_000_000


@dataclass(frozen=True)
class Routes:
    """The three answers of the service, over one recognizer and the log of its requests."""

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
        started = time.monotonic_ns()
        step = Step.RECOGNIZE
        answered = False
        try:
            found = tuple(self.recognizer.recognize(body.text, body.language))
            step = Step.RESPOND
            response = AnalyzeResponse(
                occurrences=found, model_version=self.recognizer.model_version
            )
            answered = True
        finally:
            if not answered:
                self.log.failed(body.reference, step)
        elapsed_ms = (time.monotonic_ns() - started) // NANOSECONDS_PER_MILLISECOND
        self.log.analyzed(
            AnalysisSummary(
                body.reference,
                len(body.text),
                elapsed_ms,
                response.model_version,
                Counter(occurrence.kind for occurrence in found),
            )
        )
        return response


async def refuse_invalid_request(request: Request, error: Exception) -> JSONResponse:
    """A request the contract rejects gets a bare refusal: the default would echo the input."""
    return JSONResponse({"error": "invalid_request"}, status_code=HTTP_BAD_REQUEST)
