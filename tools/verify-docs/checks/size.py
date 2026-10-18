"""Line ceiling per document."""

from __future__ import annotations

from pathlib import Path

from finding import Finding, Location

LINE_LIMIT = 60


def _is_historical(lines: list[str]) -> bool:
    return any(
        line.startswith(">") and line.lstrip("> ").startswith("Histórico") for line in lines[:12]
    )


def check_size(root: Path, path: Path, lines: list[str]) -> list[Finding]:
    """A document above the ceiling is over-justifying itself (writing rule 2).

    Two exceptions. A historical document is neither split nor summarised, because
    that would falsify the record of what was true when it was written. And a folder
    index grows one line per added document: the ceiling would make it shrink by
    hiding documents, the opposite of what an index exists to do.
    """
    if path.name == "README.md" or _is_historical(lines) or len(lines) <= LINE_LIMIT:
        return []
    message = f"{len(lines)} linhas (teto {LINE_LIMIT})"
    return [Finding.at(Location(root, path, len(lines)), "size", message)]
