"""Code rule 3 over Python sources: a service class is built only in the composition root.

A service class is one defined under ``src/`` that has a method and either receives another class
of ``src/`` in its constructor or implements a ``Protocol`` of ``src/``. ``FastAPI`` and its
registrations are the other half of the rule, in ``code_rules``.
"""

from __future__ import annotations

import ast
from collections.abc import Iterable
from dataclasses import dataclass

from support.code_rules import COMPOSITION_ROOT, Function, Source, Violation

PROTOCOL = "Protocol"
CONSTRUCTOR = "__init__"


@dataclass(frozen=True)
class ClassShape:
    """What the rule needs of one class: its name, bases, methods and constructor types."""

    name: str
    bases: frozenset[str]
    methods: frozenset[str]
    constructor_types: frozenset[str]

    @classmethod
    def of(cls, node: ast.ClassDef) -> ClassShape:
        functions = [child for child in node.body if isinstance(child, Function)]
        return cls(
            node.name,
            frozenset(name for base in node.bases for name in _names_in(base)),
            frozenset(f.name for f in functions if f.name != CONSTRUCTOR),
            frozenset(_constructor_types(node, functions)),
        )

    @property
    def is_protocol(self) -> bool:
        return PROTOCOL in self.bases


def service_classes(sources: Iterable[Source]) -> frozenset[str]:
    """The names of the service classes defined across ``sources``."""
    shapes = tuple(
        ClassShape.of(node)
        for source in sources
        for node in ast.walk(source.tree)
        if isinstance(node, ast.ClassDef)
    )
    names = frozenset(shape.name for shape in shapes)
    protocols = tuple(shape for shape in shapes if shape.is_protocol)
    return frozenset(
        shape.name
        for shape in shapes
        if not shape.is_protocol and _is_service(shape, names, protocols)
    )


def service_instantiation_outside_root(sources: Iterable[Source]) -> tuple[Violation, ...]:
    """Rule 3: every ``ServiceClass(...)`` call lives in the composition root."""
    listed = tuple(sources)
    services = service_classes(listed)
    return tuple(
        Violation(
            "service_outside_root",
            source.name,
            node.lineno,
            f"{_callee(node)}(...) built outside {COMPOSITION_ROOT}",
        )
        for source in listed
        if source.name != COMPOSITION_ROOT
        for node in ast.walk(source.tree)
        if isinstance(node, ast.Call) and _callee(node) in services
    )


def _is_service(
    shape: ClassShape, names: frozenset[str], protocols: tuple[ClassShape, ...]
) -> bool:
    if not shape.methods:
        return False
    receives_a_class = bool(shape.constructor_types & names)
    implements = any(
        protocol.name in shape.bases or (protocol.methods and protocol.methods <= shape.methods)
        for protocol in protocols
    )
    return receives_a_class or implements


def _constructor_types(node: ast.ClassDef, functions: list[Function]) -> tuple[str, ...]:
    fields = tuple(
        name
        for child in node.body
        if isinstance(child, ast.AnnAssign)
        for name in _names_in(child.annotation)
    )
    constructors = (f for f in functions if f.name == CONSTRUCTOR)
    parameters = tuple(
        name
        for constructor in constructors
        for argument in constructor.args.args[1:]
        if argument.annotation is not None
        for name in _names_in(argument.annotation)
    )
    return fields + parameters


def _names_in(annotation: ast.expr) -> tuple[str, ...]:
    """Every name an annotation mentions, so ``list[Occurrence]`` names ``Occurrence``."""
    return tuple(
        node.id if isinstance(node, ast.Name) else node.attr
        for node in ast.walk(annotation)
        if isinstance(node, ast.Name | ast.Attribute)
    )


def _callee(call: ast.Call) -> str | None:
    func = call.func
    if isinstance(func, ast.Name):
        return func.id
    if isinstance(func, ast.Attribute):
        return func.attr
    return None
