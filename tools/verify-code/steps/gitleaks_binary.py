"""The gitleaks release the verifier is pinned to, and the lookup of its verified binary."""

from __future__ import annotations

from steps.located import Located
from steps.pin import Pin
from steps.pinned_tool import PinnedTool
from steps.pinned_tool import locate as locate_tool

VERSION = "8.30.1"
RELEASE_URL = f"https://github.com/gitleaks/gitleaks/releases/download/v{VERSION}"
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
TOOL = PinnedTool("gitleaks", VERSION, RELEASE_URL, PINS)


def locate(search_path: str | None = None) -> Located:
    return locate_tool(TOOL, search_path)
