"""How the secret is compared: by ``hmac.compare_digest`` and never by an equality operator."""

import ast

from support.code_rules import Source, Violation

CONSTANT_TIME_MODULE = "hmac"
CONSTANT_TIME_FUNCTION = "compare_digest"
EQUALITY_OPERATORS = (ast.Eq, ast.NotEq)


def constant_time_comparisons(source: Source) -> int:
    """How many calls to ``hmac.compare_digest`` the source makes."""
    return sum(_is_constant_time_call(node) for node in ast.walk(source.tree))


def equality_comparisons(source: Source) -> tuple[Violation, ...]:
    """Every ``==`` or ``!=`` in the source: in the verifier, each one is a timing leak."""
    return tuple(
        Violation("equality_comparison", source.name, node.lineno, "== or != compares two values")
        for node in ast.walk(source.tree)
        if isinstance(node, ast.Compare)
        and any(isinstance(operator, EQUALITY_OPERATORS) for operator in node.ops)
    )


def _is_constant_time_call(node: ast.AST) -> bool:
    if not isinstance(node, ast.Call):
        return False
    func = node.func
    if isinstance(func, ast.Attribute):
        return func.attr == CONSTANT_TIME_FUNCTION and _is_module(func.value)
    return isinstance(func, ast.Name) and func.id == CONSTANT_TIME_FUNCTION


def _is_module(value: ast.expr) -> bool:
    return isinstance(value, ast.Name) and value.id == CONSTANT_TIME_MODULE
