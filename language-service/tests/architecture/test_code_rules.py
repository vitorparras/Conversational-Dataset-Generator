"""Code rules 2, 3 and 4 hold over src/, and each rule refuses a fixture that breaks it."""

from dataclasses import replace
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
from support.composition import service_instantiation_outside_root
from support.file_shape import ENTRY_POINT, entry_point_length, public_types_per_file

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


def test_the_entry_point_has_at_most_20_lines() -> None:
    assert check(entry_point_length, SOURCES) == ()


def test_no_production_file_declares_a_second_public_type() -> None:
    assert check(public_types_per_file, SOURCES) == ()


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


def test_entry_point_over_20_lines_is_refused() -> None:
    source = replace(fixture("entry_point_over_20_lines.py.txt"), name=ENTRY_POINT)

    violations = entry_point_length(source)

    assert [(v.rule, v.line) for v in violations] == [("entry_point_length", 1)]


def test_a_file_that_is_not_the_entry_point_may_be_longer() -> None:
    source = fixture("entry_point_over_20_lines.py.txt")

    assert entry_point_length(source) == ()


def test_a_second_public_class_is_refused_and_a_private_one_is_not() -> None:
    source = fixture("two_public_classes.py.txt")

    violations = public_types_per_file(source)

    assert [(v.rule, v.line) for v in violations] == [("public_types_per_file", 14)]


def test_no_service_is_built_outside_app() -> None:
    assert service_instantiation_outside_root(SOURCES) == ()


def test_service_with_a_constructor_dependency_built_outside_app_is_refused() -> None:
    source = fixture("service_built_outside_app.py.txt")

    violations = service_instantiation_outside_root((source,))

    assert [(v.rule, v.line) for v in violations] == [("service_outside_root", 14)]


def test_dataclass_service_built_outside_app_is_refused() -> None:
    source = fixture("dataclass_service_built_outside_app.py.txt")

    violations = service_instantiation_outside_root((source,))

    assert [(v.rule, v.line) for v in violations] == [("service_outside_root", 18)]


def test_protocol_implementation_built_outside_app_is_refused() -> None:
    source = fixture("protocol_implemented_outside_app.py.txt")

    violations = service_instantiation_outside_root((source,))

    assert [(v.rule, v.line) for v in violations] == [("service_outside_root", 13)]


def test_value_object_without_methods_is_not_a_service() -> None:
    source = fixture("value_object_built_anywhere.py.txt")

    assert service_instantiation_outside_root((source,)) == ()
