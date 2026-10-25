"""What every step shares: the context it runs in, the result it returns and the process runner."""

from __future__ import annotations

import contextlib
import os
import shutil
import signal
import subprocess
from collections.abc import Callable, Mapping, Sequence
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

OUTPUT_DIRECTORY = ".verify-code"
TELEMETRY_OPT_OUT = ("DOTNET_CLI_TELEMETRY_OPTOUT", "1")
COMMAND_TIMEOUT_SECONDS = 1800
OUTPUT_TAIL_LINES = 30
REAP_SECONDS = 5


@dataclass(frozen=True)
class CommandOutcome:
    """What a command printed, whether it exited with zero and, when it ran, its exit code."""

    ok: bool
    text: str
    code: int | None = None


RunCommand = Callable[[Sequence[str], Path], CommandOutcome]
RunFor = Callable[[Sequence[str], Path, float], CommandOutcome]


@dataclass(frozen=True)
class Context:
    """Where a step runs: the repository root, the output directory and how it runs commands."""

    root: Path
    out_dir: Path
    run: RunCommand
    base: str | None = None
    previous: Path | None = None
    run_for: RunFor = field(default_factory=lambda: run_process)


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


def run_process(
    command: Sequence[str], cwd: Path, seconds: float = COMMAND_TIMEOUT_SECONDS
) -> CommandOutcome:
    """Runs a command for at most ``seconds``; a missing tool or a timeout is a failed outcome."""
    try:
        process = subprocess.Popen(
            list(command),
            cwd=cwd,
            env=child_environment(os.environ),
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            encoding="utf-8",
            errors="replace",
            **_own_group(),
        )
    except FileNotFoundError:
        return CommandOutcome(
            False, f"'{command[0]}' não está no PATH; instale-o para rodar este passo"
        )
    try:
        stdout, stderr = process.communicate(timeout=seconds)
    except subprocess.TimeoutExpired:
        _kill_tree(process)
        with contextlib.suppress(subprocess.TimeoutExpired):
            process.communicate(timeout=REAP_SECONDS)
        _close_pipes(process)
        return CommandOutcome(False, f"'{command[0]}' passou de {seconds:.0f} s e foi interrompido")
    except BaseException:
        _kill_tree(process)
        _close_pipes(process)
        raise
    return CommandOutcome(process.returncode == 0, stdout + stderr, process.returncode)


def _own_group() -> dict[str, Any]:
    """Popen options that put the child in a process group of its own, to kill it as a group."""
    if os.name == "nt":
        return {"creationflags": subprocess.CREATE_NEW_PROCESS_GROUP}
    return {"start_new_session": True}


def _kill_tree(process: subprocess.Popen[str]) -> None:
    """Kills the child and everything it started: a grandchild left alive keeps the pipes open."""
    if os.name == "nt":
        subprocess.run(
            ["taskkill", "/F", "/T", "/PID", str(process.pid)],
            capture_output=True,
            check=False,
        )
    else:
        with contextlib.suppress(ProcessLookupError):
            os.killpg(process.pid, signal.SIGKILL)
    process.kill()
    with contextlib.suppress(subprocess.TimeoutExpired):
        process.wait(timeout=REAP_SECONDS)


def _close_pipes(process: subprocess.Popen[str]) -> None:
    for stream in (process.stdout, process.stderr):
        if stream is not None:
            stream.close()


def tail(text: str) -> tuple[str, ...]:
    """The last lines of a command's output, indented for the report."""
    lines = text.strip().splitlines()[-OUTPUT_TAIL_LINES:]
    return tuple(f"  | {line}" for line in lines)


def reset_directory(path: Path) -> None:
    """An empty directory at ``path``: results of an earlier run never count as this run's."""
    if path.exists():
        shutil.rmtree(path)
    path.mkdir(parents=True)
