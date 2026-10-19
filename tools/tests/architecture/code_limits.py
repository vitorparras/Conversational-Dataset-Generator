"""Measures the size limits of the code rules (file, function, parameters, nesting) with ``ast``."""

from __future__ import annotations

import ast
from dataclasses import dataclass
from pathlib import Path
from typing import cast

FILE_LINES = 250
FUNCTION_LINES = 40
PARAMETERS = 4
NESTING = 2

#: Parameters that name the receiver, not something the caller passes.
RECEIVERS = {"self", "cls"}

CONTROL = (
    ast.For,
    ast.AsyncFor,
    ast.While,
    ast.If,
    ast.Try,
    ast.TryStar,
    ast.With,
    ast.AsyncWith,
    ast.Match,
)


@dataclass(frozen=True)
class Violation:
    """One limit exceeded, with where it happens and by how much."""

    path: str
    line: int
    rule: str
    detail: str


def _blocks(statement: ast.stmt) -> list[list[ast.stmt]]:
    blocks: list[list[ast.stmt]] = []
    for name in ("body", "orelse", "finalbody"):
        block = cast("list[ast.stmt]", getattr(statement, name, []))
        if block:
            blocks.append(block)
    if isinstance(statement, ast.Try | ast.TryStar):
        blocks.extend(handler.body for handler in statement.handlers)
    if isinstance(statement, ast.Match):
        blocks.extend(case.body for case in statement.cases)
    return blocks


def _is_elif(statement: ast.stmt, block: list[ast.stmt]) -> bool:
    return (
        isinstance(statement, ast.If)
        and block is statement.orelse
        and len(block) == 1
        and isinstance(block[0], ast.If)
    )


def _statement_depth(statement: ast.stmt, level: int) -> int:
    if isinstance(statement, ast.FunctionDef | ast.AsyncFunctionDef):
        return _depth(statement.body, level)
    if not isinstance(statement, CONTROL):
        return level
    inner = level + 1
    depths = [inner]
    for block in _blocks(statement):
        depths.append(
            _statement_depth(block[0], level)
            if _is_elif(statement, block)
            else _depth(block, inner)
        )
    return max(depths)


def _depth(block: list[ast.stmt], level: int) -> int:
    return max((_statement_depth(statement, level) for statement in block), default=level)


def nesting(function: ast.FunctionDef | ast.AsyncFunctionDef) -> int:
    """Levels of control structures inside the function, nested functions included.

    An ``elif`` stays on its ``if`` level; a nested ``def`` adds none but keeps its host's levels.
    """
    return _depth(function.body, 0)


def parameters(function: ast.FunctionDef | ast.AsyncFunctionDef) -> int:
    """Parameters the caller passes, receiver excluded."""
    arguments = function.args
    named = [*arguments.posonlyargs, *arguments.args, *arguments.kwonlyargs]
    count = sum(1 for argument in named if argument.arg not in RECEIVERS)
    return count + (arguments.vararg is not None) + (arguments.kwarg is not None)


def _function_violations(
    path: str, function: ast.FunctionDef | ast.AsyncFunctionDef
) -> list[Violation]:
    found: list[Violation] = []
    length = (function.end_lineno or function.lineno) - function.lineno + 1
    measures = (
        ("function-lines", length, FUNCTION_LINES),
        ("parameters", parameters(function), PARAMETERS),
        ("nesting", nesting(function), NESTING),
    )
    for rule, measured, limit in measures:
        if measured > limit:
            found.append(
                Violation(path, function.lineno, rule, f"{function.name}: {measured} > {limit}")
            )
    return found


def violations(path: str, source: str) -> list[Violation]:
    """Every limit the source exceeds, the file as a whole and each function in it."""
    found: list[Violation] = []
    lines = len(source.splitlines())
    if lines > FILE_LINES:
        found.append(Violation(path, 1, "file-lines", f"{lines} > {FILE_LINES}"))
    for node in ast.walk(ast.parse(source)):
        if isinstance(node, ast.FunctionDef | ast.AsyncFunctionDef):
            found.extend(_function_violations(path, node))
    return found


def production_files(tools: Path) -> list[Path]:
    """Every Python file under ``tools`` that is not a test."""
    return sorted(
        path
        for path in tools.rglob("*.py")
        if "tests" not in path.relative_to(tools).parts and "__pycache__" not in path.parts
    )


def scan(tools: Path) -> list[Violation]:
    """Violations across the production Python files of ``tools``."""
    found: list[Violation] = []
    for path in production_files(tools):
        found.extend(
            violations(path.relative_to(tools).as_posix(), path.read_text(encoding="utf-8"))
        )
    return found
