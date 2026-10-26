"""References to files that do not exist."""

from __future__ import annotations

import re
from collections.abc import Iterator
from pathlib import Path

from document import outside_code_blocks
from finding import Finding, Location

#: Extensions that, in a cited path, indicate a reference to a repository file.
FILE_EXTENSIONS = (
    ".md",
    ".cs",
    ".py",
    ".csproj",
    ".sln",
    ".json",
    ".yml",
    ".yaml",
    ".sql",
    ".cshtml",
    ".razor",
    ".toml",
    ".cff",
    ".txt",
)

INLINE_CODE = re.compile(r"`([^`\n]+)`")


def _snippets(lines: list[str]) -> Iterator[tuple[int, str]]:
    for number, line in outside_code_blocks(lines):
        for snippet in INLINE_CODE.findall(line):
            yield number, snippet


def _cited_path(snippet: str) -> str | None:
    """The snippet as a repository path, or None when it is not one."""
    candidate = snippet.strip().rstrip(":,.;")
    if " " in candidate or candidate.startswith(("-", "$", "http")):
        return None
    # An absolute path (/usr/bin, /proc) names the host, not the repository:
    # `root / cited` would discard the root, and the verdict would follow the OS.
    if candidate.startswith("/"):
        return None
    has_slash = "/" in candidate and not candidate.endswith("/")
    if not (candidate.endswith(FILE_EXTENSIONS) or has_slash):
        return None
    # A pattern is not a literal path: glob (migrations/*.sql), naming template
    # (NNN-slug.md) and placeholder (<name>, {id}) do not exist as files.
    if "*" in candidate or "NNN" in candidate or "<" in candidate or "{" in candidate:
        return None
    return candidate


def check_references(root: Path, path: Path, lines: list[str]) -> list[Finding]:
    """Checks that every path cited in `inline code` exists in the repository.

    Only what looks like a project file path is considered: it has a slash or a
    known extension. This avoids mistaking a terminal command for a path.
    """
    return [
        Finding.at(Location(root, path, number), "references", f"`{cited}` não existe")
        for number, snippet in _snippets(lines)
        if (cited := _cited_path(snippet)) is not None and not (root / cited).exists()
    ]
