"""Says which test failed the initial run of Stryker.NET, and what the failure told.

When most of the tests fail before any mutant exists, Stryker stops with one line that cites the
tests and not the reason. The reason is in the log it writes into the output folder, so the step
shows the lines about each cited test from the console and from that log.
"""

from __future__ import annotations

import re
from pathlib import Path

MARKER = "Initial testrun has more than 50% failing tests"
LOGS = "logs"
TEST = re.compile(r"\b[A-Z]\w*Tests\.\w+")
FAILURE = re.compile(r"fail|error|exception|assert", re.IGNORECASE)
AFTER = 12
SHOWN = 60


def cited(text: str) -> tuple[str, ...]:
    """The tests the output names as ``Class.Method``, once each, in the order they come."""
    return tuple(dict.fromkeys(TEST.findall(text)))


def lines_of(output: Path, console: str) -> list[str]:
    """The console and every log file of the output folder, line by line."""
    found = console.splitlines()
    for path in sorted(output.glob(f"**/{LOGS}/*")):
        if path.is_file():
            found += path.read_text(encoding="utf-8", errors="replace").splitlines()
    return found


def about(names: tuple[str, ...], lines: list[str]) -> list[str]:
    """Each line that names a test and tells a failure, with the lines that follow it, which carry
    the message; the log also names the test when it discovers it, so the lines that only name it
    come next, and with no test named, the lines that tell a failure. A line is shown once."""
    methods = tuple(name.rpartition(".")[2] for name in names)
    named = [i for i, line in enumerate(lines) if any(m in line for m in methods)]
    failing = [i for i in named if FAILURE.search(lines[i])]
    anyfailure = [i for i, line in enumerate(lines) if FAILURE.search(line)]
    starts = failing or named or anyfailure
    chosen = (lines[j].rstrip() for i in starts for j in range(i, min(i + AFTER, len(lines))))
    return [line for line in dict.fromkeys(chosen) if line.strip()]


def explain(output: Path, console: str) -> tuple[str, ...]:
    """The report lines that name the tests of an initial run that failed and their messages; none
    when the run did not fail that way."""
    if MARKER not in console:
        return ()
    names = cited(console)
    head = f"execução inicial do Stryker reprovou; teste(s) citado(s): {', '.join(names) or '-'}"
    found = about(names, lines_of(output, console))[:SHOWN]
    return (head, *(f"  | {line}" for line in found))
