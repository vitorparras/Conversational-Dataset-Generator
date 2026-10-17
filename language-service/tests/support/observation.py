"""What a run left behind, and the scan that names every place a needle was found."""

from __future__ import annotations

from collections.abc import Mapping
from dataclasses import dataclass
from pathlib import Path


@dataclass(frozen=True)
class Evidence:
    """The two standard streams of a process and the files it left in the watched folders."""

    stdout: bytes
    stderr: bytes
    files: Mapping[str, bytes]


def snapshot(watched: Mapping[str, Path]) -> Mapping[str, bytes]:
    """Every file under each watched folder, keyed ``label/relative/path``."""
    return {
        f"{label}/{path.relative_to(root).as_posix()}": path.read_bytes()
        for label, root in watched.items()
        for path in sorted(root.rglob("*"))
        if path.is_file()
    }


def leaks(evidence: Evidence, needle: bytes) -> tuple[str, ...]:
    """The destinations that hold ``needle``: ``stdout``, ``stderr`` or ``label/file``."""
    destinations = {"stdout": evidence.stdout, "stderr": evidence.stderr, **evidence.files}
    return tuple(name for name, content in destinations.items() if needle in content)
