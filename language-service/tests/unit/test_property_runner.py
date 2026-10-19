"""The runner declares the cases and the deadlines, names the seed, and replays a failure by it."""

import random
import time
from collections.abc import Callable, Iterator
from datetime import timedelta
from itertools import count
from typing import Any

import pytest
from hypothesis import Phase, Verbosity, settings
from hypothesis import strategies as st
from hypothesis.errors import DeadlineExceeded

from support.property_budget import DECLARED, PropertyBudget
from support.property_runner import SEED_VARIABLE, PropertyRunner

BUDGET = PropertyBudget(
    cases=50, deadline=timedelta(milliseconds=200), round_deadline=timedelta(seconds=30)
)
LIMIT = 100
NUMBERS = st.integers(min_value=0, max_value=10**9)
SEED_PREFIX = "seed="


@pytest.fixture
def load_profile() -> Iterator[Callable[..., None]]:
    """Loads a Hypothesis profile for the test and restores the previous one afterwards."""
    previous = settings.get_current_profile_name()

    def load(**options: Any) -> None:
        settings.register_profile("test-profile", **options)
        settings.load_profile("test-profile")

    yield load
    settings.load_profile(previous)


def failure_of(runner: PropertyRunner) -> BaseException:
    def below_a_hundred(number: int) -> None:
        assert number < LIMIT

    with pytest.raises(AssertionError) as raised:
        runner.check(below_a_hundred, number=NUMBERS)
    return raised.value


def seed_of(failure: BaseException) -> int:
    """The seed the failure names in its notes."""
    note = next(note for note in failure.__notes__ if note.startswith(SEED_PREFIX))
    return int(note.split(";")[0].removeprefix(SEED_PREFIX))


def first_numbers(runner: PropertyRunner) -> list[int]:
    seen: list[int] = []

    def record(number: int) -> None:
        seen.append(number)

    runner.check(record, number=NUMBERS)
    return seen[:10]


def cases_run(runner: PropertyRunner) -> int:
    runs: list[int] = []

    def record(number: int) -> None:
        runs.append(number)

    runner.check(record, number=NUMBERS)
    return len(runs)


def slow_property(seconds: float) -> Callable[[int], None]:
    def too_slow(number: int) -> None:
        time.sleep(seconds)
        assert number >= 0

    return too_slow


def test_the_declared_number_of_cases_runs() -> None:
    assert cases_run(PropertyRunner(BUDGET, fixed_seed=1)) == BUDGET.cases


def test_the_runner_of_the_real_properties_runs_the_declared_cases() -> None:
    assert cases_run(PropertyRunner.from_process({})) == DECLARED.cases


def test_a_loaded_profile_does_not_shrink_the_declared_cases(
    load_profile: Callable[..., None],
) -> None:
    load_profile(max_examples=1, deadline=None)

    assert cases_run(PropertyRunner(BUDGET, fixed_seed=1)) == BUDGET.cases


def test_a_failure_names_the_seed_that_replays_it() -> None:
    failure = failure_of(PropertyRunner(BUDGET, fixed_seed=7))

    assert f"seed=7; replay it with {SEED_VARIABLE}=7" in failure.__notes__


def test_the_seed_a_failure_without_a_fixed_seed_names_repeats_the_failure() -> None:
    unseeded = failure_of(PropertyRunner(BUDGET))

    replayed = failure_of(PropertyRunner(BUDGET, fixed_seed=seed_of(unseeded)))

    assert replayed.__notes__ == unseeded.__notes__


def test_two_runs_without_a_fixed_seed_name_different_seeds_after_a_global_reset() -> None:
    random.seed(0)
    first = failure_of(PropertyRunner(BUDGET))
    random.seed(0)
    second = failure_of(PropertyRunner(BUDGET))

    assert seed_of(first) != seed_of(second)


def test_a_failure_shows_the_smallest_counterexample_and_how_to_reproduce_it() -> None:
    notes = "\n".join(failure_of(PropertyRunner(BUDGET, fixed_seed=7)).__notes__)

    assert f"number={LIMIT}" in notes
    assert "@reproduce_failure" in notes


def test_a_quiet_profile_does_not_hide_the_counterexample(
    load_profile: Callable[..., None],
) -> None:
    load_profile(verbosity=Verbosity.quiet)

    notes = "\n".join(failure_of(PropertyRunner(BUDGET, fixed_seed=7)).__notes__)

    assert f"number={LIMIT}" in notes


def test_a_profile_without_the_shrink_phase_still_shrinks(
    load_profile: Callable[..., None],
) -> None:
    load_profile(phases=[Phase.generate])

    notes = "\n".join(failure_of(PropertyRunner(BUDGET, fixed_seed=7)).__notes__)

    assert f"number={LIMIT}" in notes


def test_the_same_seed_repeats_the_failure() -> None:
    first = failure_of(PropertyRunner(BUDGET, fixed_seed=7)).__notes__
    second = failure_of(PropertyRunner(BUDGET, fixed_seed=7)).__notes__

    assert first == second


def test_the_same_seed_generates_the_same_cases() -> None:
    assert first_numbers(PropertyRunner(BUDGET, fixed_seed=3)) == first_numbers(
        PropertyRunner(BUDGET, fixed_seed=3)
    )


def test_another_seed_generates_other_cases() -> None:
    assert first_numbers(PropertyRunner(BUDGET, fixed_seed=3)) != first_numbers(
        PropertyRunner(BUDGET, fixed_seed=4)
    )


def test_the_seed_variable_fixes_the_seed_of_the_process_runner() -> None:
    from_variable = PropertyRunner.from_process({SEED_VARIABLE: "3"})

    assert first_numbers(from_variable) == first_numbers(PropertyRunner(DECLARED, 3))


def test_a_case_past_the_deadline_is_a_failure() -> None:
    with pytest.raises(DeadlineExceeded):
        PropertyRunner(BUDGET, fixed_seed=1).check(slow_property(0.4), number=st.just(1))


def test_a_case_of_the_real_properties_past_the_declared_deadline_is_a_failure() -> None:
    seconds = DECLARED.deadline.total_seconds() * 1.5

    with pytest.raises(DeadlineExceeded):
        PropertyRunner.from_process({}).check(slow_property(seconds), number=st.just(1))


def test_a_run_past_the_round_deadline_fails_naming_the_cases_and_the_limit() -> None:
    budget = PropertyBudget(BUDGET.cases, BUDGET.deadline, round_deadline=timedelta(seconds=5))
    ticks = count(step=1.0)
    runner = PropertyRunner(budget, fixed_seed=1, clock=lambda: next(ticks))

    with pytest.raises(TimeoutError, match="did not finish 50 cases within 0:00:05"):
        runner.check(slow_property(0), number=NUMBERS)
