"""Finds a tool on the PATH and checks its SHA-256 against the pinned release before any run."""

from __future__ import annotations

import hashlib
import shutil
import sys
from collections.abc import Mapping
from dataclasses import dataclass
from pathlib import Path

from steps.located import Located
from steps.pin import Pin


@dataclass(frozen=True)
class PinnedTool:
    """A tool by name, the release it is pinned to and one pin per supported platform."""

    name: str
    version: str
    release_url: str
    pins: Mapping[str, Pin]


def install_hint(tool: PinnedTool, pin: Pin) -> str:
    return (
        f"instale o {tool.name} {tool.version}: baixe {tool.release_url}/{pin.archive}, extraia o "
        f"binário para uma pasta do PATH (SHA-256 esperado do binário: {pin.sha256})"
    )


def sha256_of(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def locate(tool: PinnedTool, search_path: str | None = None) -> Located:
    """The first binary of ``tool`` on ``search_path`` (default: PATH) if its hash is the pinned."""
    pin = tool.pins.get(sys.platform)
    if pin is None:
        platforms = ", ".join(tool.pins)
        return Located(None, f"{tool.name} fixado só para {platforms}; plataforma {sys.platform}")
    found = shutil.which(tool.name, path=search_path)
    if found is None:
        return Located(None, f"'{tool.name}' não está no PATH; {install_hint(tool, pin)}")
    if sha256_of(Path(found)) != pin.sha256:
        problem = f"o {tool.name} encontrado não é o fixado ({tool.version})"
        return Located(None, f"{problem}; {install_hint(tool, pin)}")
    return Located(found)
