"""The service as a real process: started like the core starts it, watched like the core would."""

from __future__ import annotations

import json
import os
import socket
import subprocess
import sys
import time
import urllib.error
import urllib.request
from dataclasses import dataclass
from http import HTTPStatus
from pathlib import Path
from typing import Final

from support.synthetic import SECRET, SECRET_HEADER

LOOPBACK: Final = "127.0.0.1"
MODULE: Final = "language_service"
STARTUP_TIMEOUT_SECONDS: Final = 15.0
POLL_SECONDS: Final = 0.05
STOP_TIMEOUT_SECONDS: Final = 5.0


@dataclass(frozen=True)
class Exit:
    """How a process ended: its exit code, what it wrote to stderr, and how long the end took."""

    code: int | None
    stderr: str
    seconds: float


@dataclass(frozen=True)
class Place:
    """Where a process works and where it would put temporary files, both chosen by the test."""

    work: Path
    temp: Path
    home: Path

    def environment(self) -> dict[str, str]:
        temp, home = str(self.temp), self.home
        return {
            **os.environ,
            "TMPDIR": temp,
            "TEMP": temp,
            "TMP": temp,
            "HOME": str(home),
            "USERPROFILE": str(home),
            "APPDATA": str(home / "AppData" / "Roaming"),
            "LOCALAPPDATA": str(home / "AppData" / "Local"),
            "XDG_CONFIG_HOME": str(home / ".config"),
            "XDG_DATA_HOME": str(home / ".local" / "share"),
            "XDG_CACHE_HOME": str(home / ".cache"),
            "XDG_STATE_HOME": str(home / ".local" / "state"),
            "XDG_RUNTIME_DIR": str(home / "run"),
        }


@dataclass(frozen=True)
class Output:
    """Everything a finished process wrote to its two standard streams."""

    code: int | None
    stdout: bytes
    stderr: bytes


def free_port() -> int:
    """A port the system hands out as free right now."""
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as probe:
        probe.bind((LOOPBACK, 0))
        return int(probe.getsockname()[1])


def command(*arguments: str) -> list[str]:
    """The service's command line under the interpreter running the tests."""
    return [sys.executable, "-X", "utf8", "-m", MODULE, *arguments]


def start(port: int, place: Place | None = None) -> subprocess.Popen[bytes]:
    """Starts the service with its standard input piped and the secret already written on it."""
    process = subprocess.Popen(
        command("--port", str(port)),
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        cwd=place.work if place else None,
        env=place.environment() if place else None,
    )
    assert process.stdin is not None
    process.stdin.write(SECRET + b"\n")
    process.stdin.flush()
    return process


def run(*arguments: str, stdin: bytes = b"") -> Exit:
    """Runs the service to completion with ``stdin`` as its whole standard input."""
    started = time.monotonic()
    completed = subprocess.run(
        command(*arguments),
        check=False,
        input=stdin,
        capture_output=True,
        timeout=STOP_TIMEOUT_SECONDS,
    )
    return Exit(completed.returncode, completed.stderr.decode("utf-8"), time.monotonic() - started)


def wait_for_health(port: int, process: subprocess.Popen[bytes]) -> int | None:
    """Polls ``/health`` until it answers, the process dies or the startup timeout passes."""
    deadline = time.monotonic() + STARTUP_TIMEOUT_SECONDS
    while time.monotonic() < deadline and process.poll() is None:
        status = _health_status(port)
        if status is not None:
            return status
        time.sleep(POLL_SECONDS)
    return None


def close_stdin_and_wait(process: subprocess.Popen[bytes]) -> Exit:
    """Closes the write end the core would hold, as its death would, and waits for the exit."""
    started = time.monotonic()
    try:
        _, stderr = process.communicate(timeout=STOP_TIMEOUT_SECONDS)
    except subprocess.TimeoutExpired:
        process.kill()
        _, stderr = process.communicate()
    return Exit(process.returncode, stderr.decode("utf-8"), time.monotonic() - started)


def post_analyze(port: int, body: dict[str, str]) -> int:
    """Sends ``body`` to ``/analyze`` with the secret and returns the status of the answer."""
    request = urllib.request.Request(
        f"http://{LOOPBACK}:{port}/analyze",
        data=json.dumps(body).encode("utf-8"),
        headers={**SECRET_HEADER, "content-type": "application/json"},
    )
    with urllib.request.urlopen(request, timeout=STOP_TIMEOUT_SECONDS) as response:
        return int(response.status)


def finish(process: subprocess.Popen[bytes]) -> Output:
    """Closes standard input as the core's death would, and collects what the process wrote."""
    try:
        stdout, stderr = process.communicate(timeout=STOP_TIMEOUT_SECONDS)
    except subprocess.TimeoutExpired:
        process.kill()
        stdout, stderr = process.communicate()
    return Output(process.returncode, stdout, stderr)


def stop(process: subprocess.Popen[bytes]) -> None:
    """Ends a process a test left running, so a failure never leaks a listener."""
    if process.poll() is None:
        process.kill()
    process.communicate()


def _health_status(port: int) -> int | None:
    request = urllib.request.Request(f"http://{LOOPBACK}:{port}/health", headers=SECRET_HEADER)
    try:
        with urllib.request.urlopen(request, timeout=1) as response:
            return int(response.status)
    except urllib.error.HTTPError as error:
        return int(error.code)
    except (urllib.error.URLError, ConnectionError, TimeoutError):
        return None


HEALTHY: Final = int(HTTPStatus.OK)
