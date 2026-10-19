"""Runs a property over generated cases: the seed is always known and replays the same failure."""

import functools
import os
import secrets
import time
from collections.abc import Callable, Mapping
from typing import Any

from hypothesis import Phase, Verbosity, given, seed, settings
from hypothesis.strategies import SearchStrategy

from support.property_budget import DECLARED, PropertyBudget

SEED_VARIABLE = "PROPERTY_SEED"
SEED_BITS = 64


class PropertyRunner:
    """Checks a property under the declared budget; a failure carries the seed that replays it."""

    def __init__(
        self,
        budget: PropertyBudget,
        fixed_seed: int | None = None,
        clock: Callable[[], float] = time.monotonic,
    ) -> None:
        self._budget = budget
        self._fixed_seed = fixed_seed
        self._clock = clock

    @classmethod
    def from_process(cls, environment: Mapping[str, str] = os.environ) -> "PropertyRunner":
        text = environment.get(SEED_VARIABLE, "").strip()
        return cls(DECLARED, int(text) if text else None)

    def check(self, prop: Callable[..., None], **strategies: SearchStrategy[Any]) -> None:
        used = self._fixed_seed if self._fixed_seed is not None else secrets.randbits(SEED_BITS)
        declared = settings(
            max_examples=self._budget.cases,
            deadline=self._budget.deadline,
            derandomize=False,
            print_blob=True,
            database=None,
            verbosity=Verbosity.normal,
            phases=tuple(Phase),
        )
        try:
            seed(used)(declared(given(**strategies)(self._within_round(prop))))()
        except Exception as failure:
            failure.add_note(f"seed={used}; replay it with {SEED_VARIABLE}={used}")
            raise

    def _within_round(self, prop: Callable[..., None]) -> Callable[..., None]:
        """The property, failing once the whole run has taken longer than the round deadline."""
        started = self._clock()
        limit = self._budget.round_deadline

        @functools.wraps(prop)
        def guarded(*args: Any, **kwargs: Any) -> None:
            prop(*args, **kwargs)
            if self._clock() - started > limit.total_seconds():
                raise TimeoutError(
                    f"the property did not finish {self._budget.cases} cases within {limit}"
                )

        return guarded
