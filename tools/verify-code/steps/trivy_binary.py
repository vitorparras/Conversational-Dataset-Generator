"""The Trivy release the verifier is pinned to, and the lookup of its verified binary."""

from __future__ import annotations

from steps.pinned_tool import Located, Pin, PinnedTool
from steps.pinned_tool import locate as locate_tool

VERSION = "0.76.0"
RELEASE_URL = f"https://github.com/aquasecurity/trivy/releases/download/v{VERSION}"
PINS = {
    "linux": Pin(
        f"trivy_{VERSION}_Linux-64bit.tar.gz",
        "1f889576636db0623539cb339df7d06c40ebf39e7919489eafa1160e29808f19",
    ),
    "win32": Pin(
        f"trivy_{VERSION}_windows-64bit.zip",
        "51e50a34f8739424bc88282602d10945b25249ffdb51703fe3c9453c20217c42",
    ),
}
TOOL = PinnedTool("trivy", VERSION, RELEASE_URL, PINS)


def locate(search_path: str | None = None) -> Located:
    return locate_tool(TOOL, search_path)
