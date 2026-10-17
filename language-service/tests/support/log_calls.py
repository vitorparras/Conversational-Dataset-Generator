"""Code that writes to the log may carry a constant event and the fixed fields, nothing else.

The rule is a scan of ``ast`` calls: a call whose receiver is a logger, with its message, its
extra arguments and its keywords judged by shape, so an f-string, a ``%s`` argument or a field
dictionary keyed by a literal is refused at the line that wrote it.
"""

from __future__ import annotations

import ast

from support.code_rules import Source, Violation

LOG_METHODS = frozenset(
    {"debug", "info", "warning", "warn", "error", "exception", "critical", "fatal", "log"}
)
ALLOWED_KEYWORDS = frozenset({"extra"})


def log_call_content(source: Source) -> tuple[Violation, ...]:
    """Every logger call that could carry text or offsets into the log."""
    return tuple(
        Violation("log_call_content", source.name, call.lineno, reason)
        for call in ast.walk(source.tree)
        if isinstance(call, ast.Call) and _is_log_call(call)
        for reason in _reasons(call)
    )


def _is_log_call(call: ast.Call) -> bool:
    func = call.func
    return (
        isinstance(func, ast.Attribute)
        and func.attr in LOG_METHODS
        and "log" in _terminal_name(func.value).lower()
    )


def _terminal_name(node: ast.expr) -> str:
    if isinstance(node, ast.Name):
        return node.id
    if isinstance(node, ast.Attribute):
        return node.attr
    return ""


def _reasons(call: ast.Call) -> tuple[str, ...]:
    method = _terminal_name(call.func)
    arguments = call.args
    return (
        *(("exception() writes the traceback",) if method == "exception" else ()),
        *(() if _is_constant(arguments[:1]) else ("the event is not a constant",)),
        *(("arguments after the event",) if len(arguments) > 1 else ()),
        *(
            f"keyword {keyword.arg} is not allowed"
            for keyword in call.keywords
            if keyword.arg not in ALLOWED_KEYWORDS
        ),
        *(
            "extra is not keyed by constants"
            for keyword in call.keywords
            if keyword.arg == "extra" and not _keyed_by_constants(keyword.value)
        ),
    )


def _is_constant(arguments: list[ast.expr]) -> bool:
    return len(arguments) == 1 and _is_constant_name(arguments[0])


def _is_constant_name(node: ast.expr) -> bool:
    if isinstance(node, ast.Constant):
        return isinstance(node.value, str)
    name = _terminal_name(node)
    return bool(name) and name.isupper()


def _keyed_by_constants(node: ast.expr) -> bool:
    return isinstance(node, ast.Dict) and all(
        key is not None and isinstance(key, ast.Name | ast.Attribute) and _is_constant_name(key)
        for key in node.keys
    )
