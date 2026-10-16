"""Finds gitleaks on the PATH and checks its SHA-256 against the pinned release before any run."""

from __future__ import annotations

import hashlib
import shutil
import sys
from dataclasses import dataclass
from pathlib import Path

VERSION = "8.30.1"
RELEASE_URL = f"https://github.com/gitleaks/gitleaks/releases/download/v{VERSION}"


@dataclass(frozen=True)
class Pin:
    """A release archive of the pinned version and the SHA-256 of the binary inside it."""

    archive: str
    sha256: str


PINS = {
    "linux": Pin(
        f"gitleaks_{VERSION}_linux_x64.tar.gz",
        "88f91962aa2f93ac6ab281d553b9e125f5197bbbce38f9f2437f7299c32e5509",
    ),
    "win32": Pin(
        f"gitleaks_{VERSION}_windows_x64.zip",
        "17157e2ee8b76fc8b1d8bee607a250e34b8a8023c8bc81822d4b5ee4d78fcb7c",
    ),
}


@dataclass(frozen=True)
class Located:
    """The verified binary path, or the problem in Portuguese when there is none."""

    path: str | None
    problem: str = ""


def install_hint(pin: Pin) -> str:
    return (
        f"instale o gitleaks {VERSION}: baixe {RELEASE_URL}/{pin.archive}, extraia o binário "
        f"para uma pasta do PATH (SHA-256 esperado do binário: {pin.sha256})"
    )


def sha256_of(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def locate(search_path: str | None = None) -> Located:
    """The first `gitleaks` on ``search_path`` (default: PATH) if its hash is the pinned one."""
    pin = PINS.get(sys.platform)
    if pin is None:
        return Located(
            None, f"gitleaks fixado só para {', '.join(PINS)}; plataforma {sys.platform}"
        )
    found = shutil.which("gitleaks", path=search_path)
    if found is None:
        return Located(None, f"'gitleaks' não está no PATH; {install_hint(pin)}")
    if sha256_of(Path(found)) != pin.sha256:
        return Located(
            None, f"o gitleaks encontrado não é o fixado ({VERSION}); {install_hint(pin)}"
        )
    return Located(found)
