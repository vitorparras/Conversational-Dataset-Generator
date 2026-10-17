"""Code rules 2, 3 and 4 of docs/regras-de-codigo.md over Python sources, by the ``ast`` module.

Every rule takes one ``Source`` and returns the violations found in it, so the same rule runs over
the production tree and over a fixture that breaks it on purpose.
"""

from __future__ import annotations

import ast
import re
from collections.abc import Callable, Iterable
from dataclasses import dataclass
from pathlib import Path

MAX_FILE_LINES = 250
MAX_FUNCTION_LINES = 40
MAX_PARAMETERS = 4
MAX_NESTING = 2
COMPOSITION_ROOT = "app.py"
APPLICATION_CLASS = "FastAPI"
REGISTRATIONS = frozenset(
    {
        "add_api_route",
        "add_route",
        "add_middleware",
        "add_exception_handler",
        "include_router",
        "mount",
    }
)
ROUTE_DECORATORS = frozenset({"get", "post", "put", "patch", "delete", "api_route", "websocket"})
TEST_WORDS = frozenset({"test", "tests"})
RECEIVERS = frozenset({"self", "cls"})

Function = ast.FunctionDef | ast.AsyncFunctionDef
NESTING = (
    ast.If,
    ast.For,
    ast.AsyncFor,
    ast.While,
    ast.With,
    ast.AsyncWith,
    ast.Try,
    ast.TryStar,
    ast.Match,
)
SCOPES = (ast.FunctionDef, ast.AsyncFunctionDef, ast.Lambda, ast.ClassDef)


@dataclass(frozen=True)
class Source:
    """A Python file as the rules see it: its name, its text and its syntax tree."""

    name: str
    text: str
    tree: ast.Module
    path: str = ""

    @classmethod
    def read(cls, path: Path) -> Source:
        text = path.read_text(encoding="utf-8")
        return cls(path.name, text, ast.parse(text, filename=str(path)), path.as_posix())


@dataclass(frozen=True)
class Violation:
    """One breach of one rule, with the line a reader goes to."""

    rule: str
    source: str
    line: int
    detail: str


Rule = Callable[[Source], tuple[Violation, ...]]


def production_sources(root: Path) -> tuple[Source, ...]:
    """Every Python file under ``root``, in path order."""
    return tuple(Source.read(path) for path in sorted(root.rglob("*.py")))


def check(rule: Rule, sources: Iterable[Source]) -> tuple[Violation, ...]:
    """The violations of one rule over many sources."""
    return tuple(violation for source in sources for violation in rule(source))


def file_length(source: Source) -> tuple[Violation, ...]:
    """Rule 2: a file has at most 250 lines."""
    lines = len(source.text.splitlines())
    if lines <= MAX_FILE_LINES:
        return ()
    return (
        Violation("file_length", source.name, 1, f"{lines} lines; the limit is {MAX_FILE_LINES}"),
    )


def function_length(source: Source) -> tuple[Violation, ...]:
    """Rule 2: a function has at most 40 lines, decorators not counted."""
    return tuple(
        Violation(
            "function_length",
            source.name,
            function.lineno,
            f"{function.name} spans {_lines_of(function)} lines; the limit is {MAX_FUNCTION_LINES}",
        )
        for function in _functions(source)
        if _lines_of(function) > MAX_FUNCTION_LINES
    )


def parameter_count(source: Source) -> tuple[Violation, ...]:
    """Rule 2: a function takes at most 4 parameters, the receiver not counted."""
    return tuple(
        Violation(
            "parameter_count",
            source.name,
            function.lineno,
            f"{function.name} takes {_parameters_of(function)}; the limit is {MAX_PARAMETERS}",
        )
        for function in _functions(source)
        if _parameters_of(function) > MAX_PARAMETERS
    )


def nesting_depth(source: Source) -> tuple[Violation, ...]:
    """Rule 2: statements nest at most 2 levels inside a function; an ``elif`` adds none."""
    elifs = _elif_nodes(source.tree)
    return tuple(
        Violation(
            "nesting_depth",
            source.name,
            function.lineno,
            f"{function.name} nests {_depth(function, 0, elifs)} levels; limit {MAX_NESTING}",
        )
        for function in _functions(source)
        if _depth(function, 0, elifs) > MAX_NESTING
    )


def public_test_names(source: Source) -> tuple[Violation, ...]:
    """Rule 4: no public function or class is named for a test."""
    return tuple(
        Violation("public_test_names", source.name, node.lineno, f"{node.name} is named for a test")
        for node in ast.walk(source.tree)
        if isinstance(node, Function | ast.ClassDef) and _named_for_test(node.name)
    )


def composition_outside_root(source: Source) -> tuple[Violation, ...]:
    """Rule 3: the application is built and its routes registered only in the composition root."""
    if source.name == COMPOSITION_ROOT:
        return ()
    return tuple(
        sorted(
            (
                Violation("composition_outside_root", source.name, line, detail)
                for node in ast.walk(source.tree)
                for line, detail in _composition_in(node)
            ),
            key=lambda violation: violation.line,
        )
    )


def _functions(source: Source) -> tuple[Function, ...]:
    return tuple(node for node in ast.walk(source.tree) if isinstance(node, Function))


def _lines_of(function: Function) -> int:
    return (function.end_lineno or function.lineno) - function.lineno + 1


def _parameters_of(function: Function) -> int:
    arguments = function.args
    names = [*arguments.posonlyargs, *arguments.args, *arguments.kwonlyargs]
    receiver = 1 if names and names[0].arg in RECEIVERS else 0
    return len(names) + bool(arguments.vararg) + bool(arguments.kwarg) - receiver


def _elif_nodes(tree: ast.Module) -> frozenset[ast.If]:
    """The ``If`` nodes that are the sole ``else`` of another ``If``: an ``elif`` chain."""
    return frozenset(
        node.orelse[0]
        for node in ast.walk(tree)
        if isinstance(node, ast.If) and len(node.orelse) == 1 and isinstance(node.orelse[0], ast.If)
    )


def _depth(node: ast.AST, depth: int, elifs: frozenset[ast.If]) -> int:
    """The deepest nesting level under ``node``; nested scopes are measured on their own."""
    level = depth + (isinstance(node, NESTING) and node not in elifs)
    children = (child for child in ast.iter_child_nodes(node) if not isinstance(child, SCOPES))
    return max([level, *(_depth(child, level, elifs) for child in children)])


def _named_for_test(name: str) -> bool:
    if name.startswith("_"):
        return False
    words = re.findall(r"[A-Z]?[a-z0-9]+|[A-Z]+(?![a-z])", name)
    return any(word.lower() in TEST_WORDS for word in words)


def _composition_in(node: ast.AST) -> tuple[tuple[int, str], ...]:
    if isinstance(node, ast.Call) and _callee(node.func) == APPLICATION_CLASS:
        return ((node.lineno, f"{APPLICATION_CLASS}(...) built outside {COMPOSITION_ROOT}"),)
    if isinstance(node, ast.Call) and _callee(node.func) in REGISTRATIONS:
        return ((node.lineno, f"{_callee(node.func)}(...) outside {COMPOSITION_ROOT}"),)
    if isinstance(node, Function):
        return tuple(
            (
                decorator.lineno,
                f"route decorator {_callee(decorator.func)} outside {COMPOSITION_ROOT}",
            )
            for decorator in node.decorator_list
            if isinstance(decorator, ast.Call) and _callee(decorator.func) in ROUTE_DECORATORS
        )
    return ()


def _callee(func: ast.expr) -> str | None:
    if isinstance(func, ast.Name):
        return func.id
    if isinstance(func, ast.Attribute):
        return func.attr
    return None
