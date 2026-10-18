"""Runs a property over generated cases: the seed is always known and replays the same failure."""

import os
import random
from collections.abc import Callable, Mapping
from typing import Any

from hypothesis import given, seed, settings
from hypothesis.strategies import SearchStrategy

from support.property_budget import DECLARED, PropertyBudget

SEED_VARIABLE = "PROPERTY_SEED"
SEED_BITS = 64


class PropertyRunner:
    """Checks a property under the declared budget; a failure carries the seed that replays it."""

    def __init__(self, budget: PropertyBudget, fixed_seed: int | None = None) -> None:
        self._budget = budget
        self._fixed_seed = fixed_seed

    @classmethod
    def from_process(cls, environment: Mapping[str, str] = os.environ) -> "PropertyRunner":
        text = environment.get(SEED_VARIABLE, "").strip()
        return cls(DECLARED, int(text) if text else None)

    def check(self, prop: Callable[..., None], **strategies: SearchStrategy[Any]) -> None:
        used = self._fixed_seed if self._fixed_seed is not None else random.getrandbits(SEED_BITS)
        declared = settings(
            max_examples=self._budget.cases,
            deadline=self._budget.deadline,
            derandomize=False,
            print_blob=True,
            database=None,
        )
        try:
            seed(used)(declared(given(**strategies)(prop)))()
        except Exception as failure:
            failure.add_note(f"seed={used}; replay it with {SEED_VARIABLE}={used}")
            raise
