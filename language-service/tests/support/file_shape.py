"""Code rules 2 and 3 on the shape of a file: one public type each, and a short entry point."""

from __future__ import annotations

import ast

from support.code_rules import Source, Violation

MAX_ENTRY_POINT_LINES = 20
ENTRY_POINT = "__main__.py"


def entry_point_length(source: Source) -> tuple[Violation, ...]:
    """Rule 3: the entry point only calls the composition, in at most 20 lines."""
    lines = len(source.text.splitlines())
    if source.name != ENTRY_POINT or lines <= MAX_ENTRY_POINT_LINES:
        return ()
    detail = f"{lines} lines; the entry point has at most {MAX_ENTRY_POINT_LINES}"
    return (Violation("entry_point_length", source.name, 1, detail),)


def public_types_per_file(source: Source) -> tuple[Violation, ...]:
    """Rule 2: a file declares at most one public class at its top level."""
    public = [
        node
        for node in source.tree.body
        if isinstance(node, ast.ClassDef) and not node.name.startswith("_")
    ]
    return tuple(
        Violation(
            "public_types_per_file",
            source.name,
            node.lineno,
            f"{node.name} is a second public type in the file ({len(public)} in all)",
        )
        for node in public[1:]
    )
