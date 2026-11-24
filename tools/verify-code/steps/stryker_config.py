"""The configuration Stryker.NET reads for one critical core module, generated on every run."""

from __future__ import annotations

import json
from pathlib import Path

CONCURRENCY = 1


def configuration(project: str, patterns: tuple[str, ...], tests: tuple[Path, ...]) -> str:
    """The Stryker configuration of one module; Stryker never reproves, the step judges. It sets
    no method filter: that one hides the whole call chain, so what the 017 leaves out is filtered
    from the report (`read_stryker`). One worker in every module keeps the verdicts the same from
    one run to the next."""
    options = {
        "project": f"{project}.csproj",
        "test-projects": [str(path) for path in tests],
        "mutate": list(patterns),
        "test-runner": "mtp",
        "concurrency": CONCURRENCY,
        "reporters": ["json"],
        "thresholds": {"high": 100, "low": 0, "break": 0},
    }
    return json.dumps({"stryker-config": options}, indent=2) + "\n"
