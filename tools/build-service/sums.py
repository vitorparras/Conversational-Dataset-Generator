"""The SHA256SUMS manifest of the service folder, in the format of `sha256sum`.

The build writes it once, over every file but itself, with paths relative to the folder and `/` as
separator; the core reads it before it starts the service.
"""

from __future__ import annotations

import hashlib
import re
from pathlib import Path

MANIFEST = "SHA256SUMS"
LINE = re.compile(r"([0-9a-f]{64})  (.+)")
CHUNK_BYTES = 1 << 20


def files(folder: Path) -> list[str]:
    """The relative path of every file under ``folder``, but the manifest, in byte order."""
    names: list[str] = []
    for path in folder.rglob("*"):
        if path.is_symlink() and path.is_dir():
            raise ValueError(
                f"{path.relative_to(folder).as_posix()}: link para pasta fica fora da soma"
            )
        if path.is_dir():
            continue
        name = path.relative_to(folder).as_posix()
        if "\\" in name or "\n" in name:
            raise ValueError(f"{name}: o nome não cabe no formato do manifesto")
        if name != MANIFEST:
            names.append(name)
    return sorted(names, key=str.encode)


def digest(path: Path) -> str:
    """The SHA-256 of the file, read in chunks."""
    hasher = hashlib.sha256()
    with path.open("rb") as stream:
        while chunk := stream.read(CHUNK_BYTES):
            hasher.update(chunk)
    return hasher.hexdigest()


def write(folder: Path) -> int:
    """Writes the manifest of ``folder`` and returns how many files it covers."""
    names = files(folder)
    lines = (f"{digest(folder / name)}  {name}\n" for name in names)
    (folder / MANIFEST).write_bytes("".join(lines).encode("utf-8"))
    return len(names)


def read(folder: Path) -> dict[str, str]:
    """The digest of each path the manifest lists; a line out of the format is an error."""
    listed: dict[str, str] = {}
    for line in (folder / MANIFEST).read_text(encoding="utf-8").splitlines():
        match = LINE.fullmatch(line)
        if match is None:
            raise ValueError(f"{MANIFEST}: linha fora do formato do sha256sum")
        listed[match.group(2)] = match.group(1)
    return listed


def problems(folder: Path) -> list[str]:
    """Each file that changed, vanished or appeared since the manifest was written, by its path."""
    listed = read(folder)
    present = files(folder)
    on_disk = set(present)
    changed = [
        f"alterado: {name}"
        for name in present
        if name in listed and digest(folder / name) != listed[name]
    ]
    missing = [f"ausente: {name}" for name in listed if name not in on_disk]
    extra = [f"sobrando: {name}" for name in present if name not in listed]
    return [*changed, *missing, *extra]
