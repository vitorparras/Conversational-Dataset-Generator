"""The runner declares the cases and the deadline, names the seed, and replays a failure by it."""

import time
from datetime import timedelta

import pytest
from hypothesis import settings
from hypothesis import strategies as st
from hypothesis.errors import DeadlineExceeded

from support.property_budget import DECLARED, PropertyBudget
from support.property_runner import SEED_VARIABLE, PropertyRunner

BUDGET = PropertyBudget(cases=50, deadline=timedelta(milliseconds=200))
LIMIT = 100
NUMBERS = st.integers(min_value=0, max_value=10**9)


def failure_of(runner: PropertyRunner) -> BaseException:
    def below_a_hundred(number: int) -> None:
        assert number < LIMIT

    with pytest.raises(AssertionError) as raised:
        runner.check(below_a_hundred, number=NUMBERS)
    return raised.value


def first_numbers(runner: PropertyRunner) -> list[int]:
    seen: list[int] = []

    def record(number: int) -> None:
        seen.append(number)

    runner.check(record, number=NUMBERS)
    return seen[:10]


def test_the_declared_number_of_cases_runs() -> None:
    runs: list[int] = []

    def record(number: int) -> None:
        runs.append(number)

    PropertyRunner(BUDGET, fixed_seed=1).check(record, number=NUMBERS)

    assert len(runs) == BUDGET.cases


def test_a_loaded_profile_does_not_shrink_the_declared_cases() -> None:
    runs: list[int] = []

    def record(number: int) -> None:
        runs.append(number)

    settings.register_profile("one-case", max_examples=1, deadline=None)
    previous = settings.get_current_profile_name()
    settings.load_profile("one-case")
    try:
        PropertyRunner(BUDGET, fixed_seed=1).check(record, number=NUMBERS)
    finally:
        settings.load_profile(previous)

    assert len(runs) == BUDGET.cases


def test_a_failure_names_the_seed_that_replays_it() -> None:
    failure = failure_of(PropertyRunner(BUDGET, fixed_seed=7))

    assert f"seed=7; replay it with {SEED_VARIABLE}=7" in failure.__notes__


def test_a_failure_without_a_fixed_seed_still_names_one() -> None:
    failure = failure_of(PropertyRunner(BUDGET))

    assert any(note.startswith("seed=") for note in failure.__notes__)


def test_a_failure_shows_the_smallest_counterexample_and_how_to_reproduce_it() -> None:
    notes = "\n".join(failure_of(PropertyRunner(BUDGET, fixed_seed=7)).__notes__)

    assert f"number={LIMIT}" in notes
    assert "@reproduce_failure" in notes


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
    def too_slow(number: int) -> None:
        time.sleep(0.4)
        assert number >= 0

    with pytest.raises(DeadlineExceeded):
        PropertyRunner(BUDGET, fixed_seed=1).check(too_slow, number=st.just(1))
