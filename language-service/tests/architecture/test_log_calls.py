"""No production line writes text or offsets to the log, and each way of doing so is refused."""

from pathlib import Path

import pytest

from support.code_rules import Source, check, production_sources
from support.log_calls import log_call_content

TESTS = Path(__file__).resolve().parents[1]
SOURCES = production_sources(TESTS.parent / "src" / "language_service")
FIXTURES = TESTS / "fixtures" / "code_rules"
NOT_CONSTANT = "the event is not a constant"
EXTRA_OUTSIDE = "extra outside logging_/request_log.py"


def test_production_log_calls_carry_a_constant_event_and_fixed_fields() -> None:
    assert check(log_call_content, SOURCES) == ()


@pytest.mark.parametrize(
    ("name", "expected"),
    [
        ("log_text_in_fstring.py.txt", [(7, NOT_CONSTANT)]),
        ("log_text_as_argument.py.txt", [(7, "arguments after the event")]),
        (
            "log_text_in_extra.py.txt",
            [(8, EXTRA_OUTSIDE), (8, "extra is not keyed by constants")],
        ),
        ("log_exception_with_traceback.py.txt", [(11, "exception() writes the traceback")]),
        ("log_by_get_logger_call.py.txt", [(5, NOT_CONSTANT)]),
        ("log_under_alias.py.txt", [(7, NOT_CONSTANT)]),
        (
            "log_imported_by_name.py.txt",
            [(1, "log function imported by name"), (5, NOT_CONSTANT)],
        ),
        ("log_extra_outside_request_log.py.txt", [(9, EXTRA_OUTSIDE)]),
        ("log_method_assigned_to_name.py.txt", [(4, "log method taken as a value")]),
        ("log_method_by_getattr.py.txt", [(7, "log method taken as a value")]),
    ],
)
def test_log_call_that_could_carry_content_is_refused_at_its_line(
    name: str, expected: list[tuple[int, str]]
) -> None:
    violations = log_call_content(Source.read(FIXTURES / name))

    assert [(v.line, v.detail) for v in violations] == expected


def test_extra_in_a_file_named_request_log_in_another_package_is_refused(tmp_path: Path) -> None:
    other = tmp_path / "other_package" / "request_log.py"
    other.parent.mkdir()
    other.write_text(
        (FIXTURES / "log_extra_outside_request_log.py.txt").read_text("utf-8"), "utf-8"
    )

    violations = log_call_content(Source.read(other))

    assert [(v.line, v.detail) for v in violations] == [(9, EXTRA_OUTSIDE)]
