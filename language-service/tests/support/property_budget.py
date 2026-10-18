"""How many cases a property runs and how long one case may take before it fails."""

from dataclasses import dataclass
from datetime import timedelta


@dataclass(frozen=True)
class PropertyBudget:
    """The cases are declared, never traded for time; a case past the deadline is a failure."""

    cases: int
    deadline: timedelta


DECLARED = PropertyBudget(cases=200, deadline=timedelta(milliseconds=500))
