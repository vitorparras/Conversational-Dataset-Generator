"""The systems a service folder is built for, and where each keeps its interpreter."""

from __future__ import annotations

import platform
import sys
from dataclasses import dataclass

X64_MACHINES = frozenset({"x86_64", "amd64"})


@dataclass(frozen=True)
class System:
    """A target by name, the paths inside the folder that the core and the build resolve (the
    interpreter, the packages and their scripts), and the host that can build it."""

    name: str
    interpreter: str
    site_packages: str
    scripts: str
    host_platform: str

    def is_host(self) -> bool:
        """Whether this machine is the system: `uv python install` only installs for its own."""
        return sys.platform == self.host_platform and platform.machine().lower() in X64_MACHINES


LINUX_X64 = System(
    "linux-x64", "python/bin/python3", "python/lib/python3.12/site-packages", "python/bin", "linux"
)
WINDOWS_X64 = System(
    "win-x64", "python/python.exe", "python/Lib/site-packages", "python/Scripts", "win32"
)
SYSTEMS = {system.name: system for system in (LINUX_X64, WINDOWS_X64)}
