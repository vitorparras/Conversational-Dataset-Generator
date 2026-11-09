"""Runs a command that downloads and runs it again when the network fails.

    python tools/verify-code/retry.py -- pip install -r requirements.txt

The output goes through as it comes. A try that fails with a failure of the network (a 504, a reset
connection, a name that does not resolve) is followed by another, after a wait that grows; any other
failure ends the command at once. The job summary says which URL fell and with which code
(steps/downloads.py), and the exit code is the one of the last try.
"""

from __future__ import annotations

import os
import subprocess
import sys
from collections.abc import Iterator, Sequence

from steps import downloads
from steps.common import CommandOutcome, child_environment

SEPARATOR = "--"
NOT_FOUND = 127
USAGE = 2


def stream(command: Sequence[str]) -> CommandOutcome:
    """Runs the command, printing what it writes as it writes it, and keeps it for the verdict."""
    try:
        process = subprocess.Popen(
            list(command),
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            encoding="utf-8",
            errors="replace",
            env=child_environment(os.environ),
        )
    except FileNotFoundError:
        return CommandOutcome(False, f"'{command[0]}' não está no PATH", NOT_FOUND)
    with process:
        lines = list(_echo(process))
        code = process.wait()
    return CommandOutcome(code == 0, "".join(lines), code)


def _echo(process: subprocess.Popen[str]) -> Iterator[str]:
    """Each line the process writes, printed as it comes."""
    assert process.stdout is not None
    for line in process.stdout:
        print(line, end="", flush=True)
        yield line


def main(arguments: Sequence[str]) -> int:
    if SEPARATOR not in arguments or arguments.index(SEPARATOR) == len(arguments) - 1:
        print("uso: retry.py -- <comando> [argumentos]", file=sys.stderr)
        return USAGE
    command = list(arguments[arguments.index(SEPARATOR) + 1 :])
    outcome = downloads.repeat(lambda: stream(command), command)
    return 0 if outcome.ok else (outcome.code or 1)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
