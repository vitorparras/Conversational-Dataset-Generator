"""No production line writes text or offsets to the log, and each way of doing so is refused."""

from pathlib import Path

import pytest

from support.code_rules import Source, check, production_sources
from support.log_calls import log_call_content

TESTS = Path(__file__).resolve().parents[1]
SOURCES = production_sources(TESTS.parent / "src" / "language_service")
FIXTURES = TESTS / "fixtures" / "code_rules"


def test_production_log_calls_carry_a_constant_event_and_fixed_fields() -> None:
    assert check(log_call_content, SOURCES) == ()


@pytest.mark.parametrize(
    ("name", "line", "reason"),
    [
        ("log_text_in_fstring.py.txt", 7, "the event is not a constant"),
        ("log_text_as_argument.py.txt", 7, "arguments after the event"),
        ("log_text_in_extra.py.txt", 8, "extra is not keyed by constants"),
        ("log_exception_with_traceback.py.txt", 11, "exception() writes the traceback"),
    ],
)
def test_log_call_that_could_carry_content_is_refused_at_its_line(
    name: str, line: int, reason: str
) -> None:
    violations = log_call_content(Source.read(FIXTURES / name))

    assert [(v.line, v.detail) for v in violations] == [(line, reason)]
