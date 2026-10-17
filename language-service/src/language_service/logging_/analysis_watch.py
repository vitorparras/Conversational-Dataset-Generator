"""The state of a request in flight: the step it has reached and, once it has one, its answer."""

from dataclasses import dataclass

from language_service.contract import AnalyzeResponse
from language_service.logging_.step import Step


@dataclass
class AnalysisWatch:
    """Holds no behaviour: the route moves ``step`` forward and sets ``response`` at the end."""

    step: Step = Step.RECOGNIZE
    response: AnalyzeResponse | None = None
