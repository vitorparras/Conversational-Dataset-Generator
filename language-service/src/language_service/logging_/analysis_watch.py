"""The state of a request in flight: the step it has reached and, once it has one, its answer."""

from language_service.contract import AnalyzeResponse
from language_service.logging_.step import Step


class AnalysisWatch:
    """Moves forward only through ``reach`` and ``answer``; the log reads where it stopped."""

    def __init__(self) -> None:
        self._step = Step.RECOGNIZE
        self._response: AnalyzeResponse | None = None

    @property
    def step(self) -> Step:
        return self._step

    @property
    def response(self) -> AnalyzeResponse | None:
        return self._response

    def reach(self, step: Step) -> None:
        self._step = step

    def answer(self, response: AnalyzeResponse) -> AnalyzeResponse:
        """Records ``response`` as the answer of the request and hands it back."""
        self._response = response
        return response
