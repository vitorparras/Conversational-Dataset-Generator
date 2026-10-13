"""What every step shares: the context it runs in, the result it returns and the process runner."""

from __future__ import annotations

import os
import shutil
import subprocess
from collections.abc import Callable, Mapping, Sequence
from dataclasses import dataclass
from pathlib import Path

OUTPUT_DIRECTORY = ".verify-code"
TELEMETRY_OPT_OUT = ("DOTNET_CLI_TELEMETRY_OPTOUT", "1")
COMMAND_TIMEOUT_SECONDS = 1800
OUTPUT_TAIL_LINES = 30


@dataclass(frozen=True)
class CommandOutcome:
    """What a command printed, and whether it exited with zero."""

    ok: bool
    text: str


RunCommand = Callable[[Sequence[str], Path], CommandOutcome]


@dataclass(frozen=True)
class Context:
    """Where a step runs: the repository root, the output directory and how it runs commands."""

    root: Path
    out_dir: Path
    run: RunCommand
    base: str | None = None


@dataclass(frozen=True)
class StepResult:
    """The outcome of one step: pass or fail, with the lines a reader needs to act on it."""

    name: str
    ok: bool
    details: tuple[str, ...] = ()
    elapsed: float = 0.0


def passed(name: str, *details: str) -> StepResult:
    return StepResult(name, True, details)


def failed(name: str, *details: str) -> StepResult:
    return StepResult(name, False, details)


def child_environment(parent: Mapping[str, str]) -> dict[str, str]:
    """The parent's environment plus the telemetry opt-out of the .NET tooling."""
    environment = dict(parent)
    environment[TELEMETRY_OPT_OUT[0]] = TELEMETRY_OPT_OUT[1]
    return environment


def run_process(command: Sequence[str], cwd: Path) -> CommandOutcome:
    """Runs a command and captures its output; a missing tool or a timeout is a failed outcome."""
    try:
        completed = subprocess.run(
            list(command), cwd=cwd, env=child_environment(os.environ), capture_output=True,
            text=True, encoding="utf-8", errors="replace", timeout=COMMAND_TIMEOUT_SECONDS,
            check=False,
        )
    except FileNotFoundError:
        return CommandOutcome(False, f"'{command[0]}' não está no PATH; instale-o para rodar este passo")
    except subprocess.TimeoutExpired:
        return CommandOutcome(False, f"'{command[0]}' passou de {COMMAND_TIMEOUT_SECONDS} s e foi interrompido")
    return CommandOutcome(completed.returncode == 0, completed.stdout + completed.stderr)


def tail(text: str) -> tuple[str, ...]:
    """The last lines of a command's output, indented for the report."""
    lines = text.strip().splitlines()[-OUTPUT_TAIL_LINES:]
    return tuple(f"  | {line}" for line in lines)


def reset_directory(path: Path) -> None:
    """An empty directory at ``path``: results of an earlier run never count as this run's."""
    if path.exists():
        shutil.rmtree(path)
    path.mkdir(parents=True)
