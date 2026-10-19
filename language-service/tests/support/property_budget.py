"""How many cases a property runs and how long a case and the whole run may take before failing."""

from dataclasses import dataclass
from datetime import timedelta


@dataclass(frozen=True)
class PropertyBudget:
    """The cases are declared, never traded for time; a case or a run past its deadline fails."""

    cases: int
    deadline: timedelta
    round_deadline: timedelta


DECLARED = PropertyBudget(
    cases=200, deadline=timedelta(milliseconds=500), round_deadline=timedelta(seconds=30)
)
