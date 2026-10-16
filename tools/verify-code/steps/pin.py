"""A release archive of a pinned tool and the SHA-256 of the binary inside it."""

from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class Pin:
    """The archive to download and the hash its binary must have."""

    archive: str
    sha256: str
