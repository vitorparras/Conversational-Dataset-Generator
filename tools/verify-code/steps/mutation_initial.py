"""Says which test failed the initial run of Stryker.NET, and what the failure told.

When the initial run fails before any mutant exists, Stryker stops with one line that cites the
test and its state, not the reason. The reason is in the log of the JSON-RPC talk it keeps with the
test host (``logs/test-servers/rpc-*.log``, written with ``--log-to-file``): each failed test comes
there as a node with its ``error.message`` and ``error.stacktrace``. The step reads those nodes, and
only when that talk holds none does it show the lines about each cited test from the console and
from the other logs.
"""

from __future__ import annotations

import json
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Any, cast

MARKER = "Initial testrun has more than 50% failing tests"
LOGS = "logs"
RPC = "test-servers/rpc-*.log"
RECEIVED = "Received: "
FAILED_STATES = frozenset({"error", "failed", "timed-out"})
TEST = re.compile(r"\b[A-Z]\w*Tests\.\w+")
FAILURE = re.compile(r"fail|error|exception|assert", re.IGNORECASE)
AFTER = 12
SHOWN = 60
MESSAGE_LINES = 15
STACK_LINES = 8


@dataclass(frozen=True)
class FailedTest:
    """A test the host told as failed: its display name, state, message and stack trace."""

    name: str
    state: str
    message: str
    stack: str


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


def _mapping(value: object) -> dict[str, Any]:
    """``value`` as a JSON object, or an empty one when it is anything else."""
    return cast("dict[str, Any]", value) if isinstance(value, dict) else {}


def _nodes(line: str) -> list[dict[str, Any]]:
    """The test nodes a ``Received:`` line of the talk carries; none for any other line."""
    _, mark, payload = line.partition(RECEIVED)
    if not mark:
        return []
    try:
        body = _mapping(json.loads(payload))
    except ValueError:
        return []
    changes = _mapping(body.get("params")).get("changes")
    items = cast("list[Any]", changes) if isinstance(changes, list) else []
    nodes = (_mapping(_mapping(item).get("node")) for item in items)
    return [node for node in nodes if node]


def _failure_of(node: dict[str, Any]) -> FailedTest | None:
    """The test a node tells as failed; None for a node in any other state."""
    state = str(node.get("execution-state", ""))
    if state not in FAILED_STATES:
        return None
    name = str(node.get("display-name", node.get("uid", "?")))
    return FailedTest(
        name, state, str(node.get("error.message", "")), str(node.get("error.stacktrace", ""))
    )


def failed_tests(output: Path) -> tuple[FailedTest, ...]:
    """The tests the host told as failed in the talk logs of the output folder, once each (the last
    word on a test stands), in the order they first came."""
    lines = (
        line
        for path in sorted(output.glob(f"**/{LOGS}/{RPC}"))
        for line in path.read_text(encoding="utf-8", errors="replace").splitlines()
    )
    failures = (_failure_of(node) for line in lines for node in _nodes(line))
    found = {failure.name: failure for failure in failures if failure is not None}
    return tuple(found.values())


def _head(text: str, count: int) -> list[str]:
    """The first ``count`` non-blank lines of ``text``."""
    return [line.rstrip() for line in text.splitlines() if line.strip()][:count]


def explain(output: Path, console: str) -> tuple[str, ...]:
    """The report lines that name the tests of an initial run that failed and their messages; none
    when the run did not fail that way."""
    if MARKER not in console:
        return ()
    names = cited(console)
    failures = failed_tests(output)
    shown = tuple(failure.name for failure in failures) or names
    head = f"execução inicial do Stryker reprovou; teste(s) citado(s): {', '.join(shown) or '-'}"
    if failures:
        told = (
            line
            for failure in failures
            for line in (
                f"{failure.name} (estado {failure.state})",
                *(f"  {line}" for line in _head(failure.message, MESSAGE_LINES)),
                *(f"  {line.strip()}" for line in _head(failure.stack, STACK_LINES)),
            )
        )
        return (head, *(f"  | {line}" for line in told))
    found = about(names, lines_of(output, console))[:SHOWN]
    return (head, *(f"  | {line}" for line in found))


def summary(output: Path, console: str, module: str) -> str:
    """The Markdown for the job summary: each test the host told as failed in the initial run, with
    its message and the head of its stack; empty when the run did not fail that way or told none."""
    failures = failed_tests(output) if MARKER in console else ()
    if not failures:
        return ""
    parts = [f"### Mutação ({module}): a execução inicial do Stryker reprovou\n"]
    for failure in failures:
        message = "\n".join(_head(failure.message, MESSAGE_LINES)) or "(sem mensagem)"
        stack = "\n".join(_head(failure.stack, STACK_LINES))
        parts.append(
            f"**`{failure.name}`**, estado `{failure.state}`\n\n```text\n{message}\n{stack}\n```\n"
        )
    return "\n".join(parts) + "\n"
