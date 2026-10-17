"""Code that writes to the log may carry a constant event and the fixed fields, nothing else.

The rule is a scan of ``ast`` calls judged by the method they call, never by the name of what they
are called on: a logger fetched in place, kept under any alias or imported by name is still seen.
"""

from __future__ import annotations

import ast

from support.code_rules import Source, Violation

LOG_METHODS = frozenset(
    {"debug", "info", "warning", "warn", "error", "exception", "critical", "fatal", "log"}
)
LOGGING_MODULE = "logging"
EXTRA_HOME = "request_log.py"
EXTRA = "extra"


def log_call_content(source: Source) -> tuple[Violation, ...]:
    """Every logger call, and every import of a log function by name, that could carry content."""
    calls = (
        Violation("log_call_content", source.name, node.lineno, reason)
        for node in ast.walk(source.tree)
        if isinstance(node, ast.Call) and _method_of(node) in LOG_METHODS
        for reason in _reasons(node, source.name)
    )
    imports = (
        Violation("log_call_content", source.name, node.lineno, "log function imported by name")
        for node in ast.walk(source.tree)
        if isinstance(node, ast.ImportFrom) and node.module == LOGGING_MODULE
    )
    return tuple(sorted((*calls, *imports), key=lambda violation: violation.line))


def _method_of(call: ast.Call) -> str:
    func = call.func
    if isinstance(func, ast.Attribute):
        return func.attr
    if isinstance(func, ast.Name):
        return func.id
    return ""


def _reasons(call: ast.Call, source_name: str) -> tuple[str, ...]:
    method = _method_of(call)
    arguments = call.args[1:] if method == "log" else call.args
    extras = [keyword for keyword in call.keywords if keyword.arg == EXTRA]
    others = [keyword for keyword in call.keywords if keyword.arg != EXTRA]
    return (
        *(("exception() writes the traceback",) if method == "exception" else ()),
        *(() if _is_constant_event(arguments[:1]) else ("the event is not a constant",)),
        *(("arguments after the event",) if len(arguments) > 1 else ()),
        *(f"keyword {keyword.arg} is not allowed" for keyword in others),
        *(f"{EXTRA} outside {EXTRA_HOME}" for _ in extras if source_name != EXTRA_HOME),
        *("extra is not keyed by constants" for e in extras if not _keyed_by_constants(e.value)),
    )


def _is_constant_event(arguments: list[ast.expr]) -> bool:
    return len(arguments) == 1 and _is_constant_name(arguments[0])


def _is_constant_name(node: ast.expr) -> bool:
    if isinstance(node, ast.Constant):
        return isinstance(node.value, str)
    name = node.id if isinstance(node, ast.Name) else getattr(node, "attr", "")
    return bool(name) and name.isupper()


def _keyed_by_constants(node: ast.expr) -> bool:
    return isinstance(node, ast.Dict) and all(
        isinstance(key, ast.Name | ast.Attribute) and _is_constant_name(key) for key in node.keys
    )
