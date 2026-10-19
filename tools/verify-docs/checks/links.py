"""Broken internal links."""

from __future__ import annotations

import re
from pathlib import Path

from document import outside_code_blocks
from finding import Finding, Location

LINK = re.compile(r"\[[^\]]*\]\(([^)]+)\)")
EXTERNAL = ("http://", "https://", "mailto:", "#")


def _is_broken(path: Path, target: str) -> bool:
    if target.startswith(EXTERNAL):
        return False
    return not (path.parent / target.split("#", 1)[0]).resolve().exists()


def check_links(root: Path, path: Path, lines: list[str]) -> list[Finding]:
    """Checks that every relative link resolves. Ignores external links and anchors."""
    return [
        Finding.at(Location(root, path, number), "links", f"link quebrado: {target}")
        for number, line in outside_code_blocks(lines)
        for target in LINK.findall(line)
        if _is_broken(path, target)
    ]
