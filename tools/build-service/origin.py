"""The ORIGIN.json file of the service folder: where the folder came from, in the folder itself.

It names the commit the build ran on, whether the working tree held changes not committed, and the
versions the folder carries: interpreter, service and model package. A dirty tree is not refused;
the file says so, which lets whoever holds the folder tell it from one built from the commit.
The manifest covers the file, so changing what it declares is changing the folder.
"""

from __future__ import annotations

import json
import subprocess
import tomllib
from dataclasses import asdict, dataclass
from pathlib import Path

FILE = "ORIGIN.json"
CLEAN = "clean"
DIRTY = "dirty"
LOCK = "uv.lock"
PROJECT = "pyproject.toml"
UNKNOWN = "git não respondeu: não se sabe de que commit a pasta sairia"


class OriginError(Exception):
    """What keeps the origin from being known, in words for whoever runs the build."""


@dataclass(frozen=True)
class Origin:
    """What the folder declares about itself; the field names are the keys of the file."""

    commit: str
    tree: str
    system: str
    python: str
    service: str
    model: str
    model_version: str


def git(root: Path, *arguments: str) -> str:
    """The output of a git command in ``root``; a failure to run it is an OriginError."""
    try:
        done = subprocess.run(
            ["git", "-C", str(root), *arguments], capture_output=True, text=True, check=False
        )
    except OSError as error:
        raise OriginError(f"{UNKNOWN}: {error}") from error
    if done.returncode != 0:
        raise OriginError(f"{UNKNOWN}: {done.stderr.strip()}")
    return done.stdout


def tree_state(root: Path) -> str:
    """``dirty`` when a tracked file changed or an untracked one is not ignored, else ``clean``."""
    return DIRTY if git(root, "status", "--porcelain").strip() else CLEAN


def locked_version(service: Path, package: str) -> str:
    """The version the lock of the service resolves for ``package``."""
    lock = tomllib.loads((service / LOCK).read_text(encoding="utf-8"))
    found = [p["version"] for p in lock.get("package", []) if p.get("name") == package]
    if len(found) != 1:
        raise OriginError(f"{service / LOCK}: o pacote {package} não tem uma versão só")
    return str(found[0])


def service_version(service: Path) -> str:
    """The version the service declares in its project file."""
    project = tomllib.loads((service / PROJECT).read_text(encoding="utf-8"))
    return str(project["project"]["version"])


@dataclass(frozen=True)
class Build:
    """What the build chose: the system, the interpreter version and the model by package name."""

    system: str
    python: str
    model: str


def read(root: Path, service: Path, build: Build) -> Origin:
    """The origin of a build of ``service``, as the tree at ``root`` holds it right now.

    Read it before anything is installed: building the service may leave files in the tree.
    """
    package = build.model.replace("_", "-")
    return Origin(
        commit=git(root, "rev-parse", "HEAD").strip(),
        tree=tree_state(root),
        system=build.system,
        python=build.python,
        service=service_version(service),
        model=package,
        model_version=locked_version(service, package),
    )


def render(origin: Origin) -> bytes:
    """The bytes of the file: keys in order, one per line, UTF-8 and a final newline."""
    return (json.dumps(asdict(origin), indent=2, sort_keys=True) + "\n").encode("utf-8")


def write(folder: Path, origin: Origin) -> None:
    """Writes the file at the root of ``folder``."""
    (folder / FILE).write_bytes(render(origin))
