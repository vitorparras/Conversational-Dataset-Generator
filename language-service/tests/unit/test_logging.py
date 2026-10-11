"""Issue 33: the formatter takes only the closed fields, and only values with their shape."""

import io
import json

import pytest

from language_service.logging_ import (
    ALLOWED_FIELDS,
    ForbiddenLogFieldError,
    InvalidLogValueError,
    ServiceLog,
    format_event,
)

GOOD = {
    "reference": "ref-1",
    "chars": 10,
    "elapsed_ms": 1.5,
    "model_version": "m-1.0",
    "count_by_kind": {"person": 2},
    "step": "respond",
    "outcome": "ok",
}


def test_the_closed_set_is_exactly_the_one_of_issue_33() -> None:
    assert ALLOWED_FIELDS == {
        "reference", "chars", "elapsed_ms", "model_version", "count_by_kind", "step", "outcome"
    }


def test_a_full_event_is_one_json_line() -> None:
    line = format_event(**GOOD)

    assert "\n" not in line
    assert json.loads(line) == GOOD


@pytest.mark.parametrize("name", ["text", "message", "msg", "start", "end", "offsets", "occurrences", "body", "detail"])
def test_a_field_outside_the_set_is_refused_by_name(name: str) -> None:
    with pytest.raises(ForbiddenLogFieldError, match=name):
        format_event(**{**GOOD, name: "ZQX-MARKER-7731"})


def test_the_refusal_does_not_repeat_the_value() -> None:
    with pytest.raises(ForbiddenLogFieldError) as raised:
        format_event(text="ZQX-MARKER-7731")

    assert "ZQX-MARKER-7731" not in str(raised.value)


@pytest.mark.parametrize(
    ("name", "value"),
    [
        ("reference", "a sentence with spaces"),
        ("reference", "x" * 65),
        ("reference", ""),
        ("reference", None),
        ("chars", -1),
        ("chars", True),
        ("chars", "10"),
        ("elapsed_ms", float("nan")),
        ("elapsed_ms", float("inf")),
        ("elapsed_ms", -0.1),
        ("model_version", "a sentence"),
        ("count_by_kind", {"a sentence": 1}),
        ("count_by_kind", {"person": -1}),
        ("count_by_kind", {"person": "ZQX-MARKER-7731"}),
        ("count_by_kind", ["person"]),
        ("step", "analysing ZQX-MARKER-7731"),
        ("step", "unknown"),
        ("outcome", "failed on ZQX-MARKER-7731"),
        ("outcome", "error:"),
        ("outcome", "error:Not A Type"),
    ],
)
def test_a_value_without_the_shape_of_its_field_is_refused(name: str, value: object) -> None:
    with pytest.raises(InvalidLogValueError, match=name) as raised:
        format_event(**{**GOOD, name: value})

    assert "ZQX-MARKER-7731" not in str(raised.value)


@pytest.mark.parametrize("outcome", ["ok", "refused", "error:ValueError", "error:RuntimeError"])
def test_the_three_outcomes_are_accepted(outcome: str) -> None:
    assert json.loads(format_event(step="auth", outcome=outcome))["outcome"] == outcome


def test_a_partial_event_is_allowed() -> None:
    assert json.loads(format_event(reference="r", step="auth", outcome="refused")) == {
        "reference": "r",
        "step": "auth",
        "outcome": "refused",
    }


def test_the_log_writes_every_sink() -> None:
    first, second = io.StringIO(), io.StringIO()

    ServiceLog([first, second]).event(**GOOD)

    assert first.getvalue() == second.getvalue()
    assert first.getvalue().endswith("\n")
    assert json.loads(first.getvalue()) == GOOD


def test_a_refused_event_writes_nothing() -> None:
    sink = io.StringIO()

    with pytest.raises(ForbiddenLogFieldError):
        ServiceLog([sink]).event(reference="r", text="x")

    assert sink.getvalue() == ""
