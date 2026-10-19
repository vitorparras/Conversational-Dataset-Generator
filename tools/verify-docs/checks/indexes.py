"""Folder indexes: every numbered document appears in the README of its folder."""

from __future__ import annotations

import re
from pathlib import Path

from finding import Finding, Location

CITED_DOCUMENT = re.compile(r"\]\(([^)]*\.md)\)")


def _check_folder(root: Path, folder: Path) -> list[Finding]:
    index = folder / "README.md"
    if not folder.is_dir() or not index.exists():
        return []
    on_disk = {f.name for f in folder.glob("*.md") if f.name != "README.md" and f.name[0].isdigit()}
    if not on_disk:
        return []
    cited = set(CITED_DOCUMENT.findall(index.read_text(encoding="utf-8")))
    where = Location(root, index, 1)
    missing = [
        Finding.at(where, "index", f"{name} existe e não está no índice")
        for name in sorted(on_disk - cited)
    ]
    dead = [
        Finding.at(where, "index", f"o índice cita {name}, que não existe")
        for name in sorted(c for c in cited if c and c[0].isdigit())
        if name not in on_disk
    ]
    return missing + dead


def check_indexes(root: Path) -> list[Finding]:
    """A document missing from its folder index is a document nobody finds.

    This check is per folder, not per file: it compares what exists on disk with
    what the README lists. The failure it catches is silent - whoever adds a
    document and forgets the index gets no error. The inverse also holds: an index
    that cites a deleted file sends the reader to a dead link.
    """
    docs = root / "docs"
    if not docs.is_dir():
        return []
    return [found for folder in sorted(docs.iterdir()) for found in _check_folder(root, folder)]
