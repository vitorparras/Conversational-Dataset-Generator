"""The configuration Stryker.NET reads for one critical core module, generated on every run."""

from __future__ import annotations

import json
import re
from pathlib import Path

from steps.mutation_cost import SUITE_WIDE
from steps.mutation_members import UNBOUNDED, slices
from steps.mutation_parts import Slice, bare

CONCURRENCY = 1
SPAN = re.compile(r"\{(\d+)\.\.(\d+)\}")
#: The project from the test project Stryker gets: ``core/tests/<Tests>/<Tests>.csproj``.
TEST_DEPTH = 2


def configuration(project: str, patterns: tuple[str, ...], tests: tuple[Path, ...]) -> str:
    """The Stryker configuration of one module; Stryker never reproves, the step judges. It sets
    no method filter: that one hides the whole call chain, so what the 017 leaves out is filtered
    from the report (`read_stryker`). One worker in every module keeps the verdicts the same from
    one run to the next. A part whose every slice is of the suite or inert skips the coverage of
    each test: Stryker runs the whole suite for each of those mutants anyway (#935)."""
    options = {
        "project": f"{project}.csproj",
        "test-projects": [str(path) for path in tests],
        "mutate": list(patterns),
        "test-runner": "mtp",
        "concurrency": CONCURRENCY,
        "reporters": ["json"],
        "thresholds": {"high": 100, "low": 0, "break": 0},
    }
    if tests and suite_only(tests[0].parents[TEST_DEPTH] / "src" / project, patterns):
        options["coverage-analysis"] = "off"
    return json.dumps({"stryker-config": options}, indent=2) + "\n"


def suite_only(project: Path, patterns: tuple[str, ...]) -> bool:
    """Whether the slices of files of ``project`` the patterns name are all of the suite or inert:
    a file whole or the spans of a part; never for a glob, which names a whole area, nor for a
    file that is not there."""
    held: list[Slice] = []
    for pattern in patterns:
        name = pattern.partition("{")[0]
        path = project / name
        if "*" in name or name.startswith("!") or not path.is_file():
            return False
        spans = [(int(a), int(b)) for a, b in SPAN.findall(pattern)] or [(0, UNBOUNDED)]
        cut = slices(path.read_bytes().decode("utf-8-sig"), SUITE_WIDE.get(name, frozenset()))
        held += [
            Slice(name, a, b, w) for a, b, w in cut if any(s <= a and b <= e for s, e in spans)
        ]
    return bool(held) and bare(held)
