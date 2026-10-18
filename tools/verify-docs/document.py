"""Finding the documents under a root and reading them the way the checks need."""

from __future__ import annotations

from pathlib import Path

#: Directories that are not project documentation.
IGNORED = {".git", ".github", ".specs", "tools", "node_modules", "bin", "obj", ".venv"}


def list_documents(root: Path) -> list[Path]:
    """Every .md under the root, outside the ignored directories."""
    found: list[Path] = []
    for path in root.rglob("*.md"):
        if any(part in IGNORED for part in path.relative_to(root).parts[:-1]):
            continue
        found.append(path)
    return sorted(found)


def outside_code_blocks(lines: list[str]) -> list[tuple[int, str]]:
    """Numbered lines (1-based), excluding blocks fenced by three backticks.

    A code block holds examples - including made-up paths and demo secrets -
    and checking its content would produce false positives.
    """
    outside: list[tuple[int, str]] = []
    inside_block = False
    for number, line in enumerate(lines, start=1):
        if line.lstrip().startswith("```"):
            inside_block = not inside_block
            continue
        if not inside_block:
            outside.append((number, line))
    return outside
