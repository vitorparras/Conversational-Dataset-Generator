"""Code rules 2, 3 and 4 hold over src/, and each rule refuses a fixture that breaks it."""

from pathlib import Path

from support.code_rules import (
    Source,
    check,
    composition_outside_root,
    file_length,
    function_length,
    nesting_depth,
    parameter_count,
    production_sources,
    public_test_names,
)

TESTS = Path(__file__).resolve().parents[1]
SOURCES = production_sources(TESTS.parent / "src" / "language_service")
FIXTURES = TESTS / "fixtures" / "code_rules"


def fixture(name: str) -> Source:
    return Source.read(FIXTURES / name)


def test_production_sources_are_found() -> None:
    names = {source.name for source in SOURCES}

    assert {"__main__.py", "app.py"} <= names


def test_no_production_file_exceeds_250_lines() -> None:
    assert check(file_length, SOURCES) == ()


def test_no_production_function_exceeds_40_lines() -> None:
    assert check(function_length, SOURCES) == ()


def test_no_production_function_takes_more_than_4_parameters() -> None:
    assert check(parameter_count, SOURCES) == ()


def test_no_production_function_nests_deeper_than_2_levels() -> None:
    assert check(nesting_depth, SOURCES) == ()


def test_no_public_name_in_production_is_named_for_a_test() -> None:
    assert check(public_test_names, SOURCES) == ()


def test_composition_happens_only_in_app() -> None:
    assert check(composition_outside_root, SOURCES) == ()


def test_file_over_250_lines_is_refused() -> None:
    source = fixture("file_over_250_lines.py.txt")

    violations = file_length(source)

    assert [v.rule for v in violations] == ["file_length"]


def test_function_over_40_lines_is_refused() -> None:
    source = fixture("function_over_40_lines.py.txt")

    violations = function_length(source)

    assert [(v.rule, v.line) for v in violations] == [("function_length", 1)]


def test_function_with_five_parameters_is_refused() -> None:
    source = fixture("five_parameters.py.txt")

    violations = parameter_count(source)

    assert [(v.rule, v.line) for v in violations] == [("parameter_count", 6)]


def test_three_nesting_levels_are_refused_and_an_elif_chain_is_not() -> None:
    source = fixture("nesting_three_levels.py.txt")

    violations = nesting_depth(source)

    assert [(v.rule, v.line) for v in violations] == [("nesting_depth", 1)]


def test_public_name_for_a_test_is_refused() -> None:
    source = fixture("public_name_for_test.py.txt")

    violations = public_test_names(source)

    assert [(v.rule, v.line) for v in violations] == [("public_test_names", 8)]


def test_composition_outside_app_is_refused() -> None:
    source = fixture("composition_outside_app.py.txt")

    violations = composition_outside_root(source)

    assert [v.line for v in violations] == [4, 7, 11]
