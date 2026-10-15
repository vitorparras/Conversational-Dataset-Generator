"""The verifier compares secrets only in constant time; an equality operator is refused."""

from pathlib import Path

from support.code_rules import Source
from support.constant_time import constant_time_comparisons, equality_comparisons

TESTS = Path(__file__).resolve().parents[1]
AUTH = Source.read(TESTS.parent / "src" / "language_service" / "auth.py")
FIXTURE = Source.read(TESTS / "fixtures" / "code_rules" / "secret_compared_with_equals.py.txt")


def test_auth_compares_through_hmac_compare_digest() -> None:
    assert constant_time_comparisons(AUTH) == 1


def test_auth_has_no_equality_comparison() -> None:
    assert equality_comparisons(AUTH) == ()


def test_verifier_built_on_equals_has_no_constant_time_comparison() -> None:
    assert constant_time_comparisons(FIXTURE) == 0


def test_verifier_built_on_equals_is_refused() -> None:
    violations = equality_comparisons(FIXTURE)

    assert [(v.rule, v.line) for v in violations] == [("equality_comparison", 12)]
