"""Starts the real service in a child process, the way the core does: secret on standard input."""

import os
import secrets
import socket
import subprocess
import sys
import tempfile
import threading
import time
from collections.abc import Mapping
from pathlib import Path
from types import TracebackType

import httpx

SECRET_HEADER = "X-Service-Secret"


def free_port() -> int:
    """A port nothing listens on right now."""
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as probe:
        probe.bind(("127.0.0.1", 0))
        return int(probe.getsockname()[1])


def new_secret() -> str:
    """A secret of the kind the core generates: 32 random bytes, written as hexadecimal."""
    return secrets.token_hex(32)


class RunningService:
    """The service as a child process, with everything it wrote kept for inspection."""

    def __init__(
        self,
        *,
        secret: str | None = None,
        port: int | None = None,
        module: str = "language_service",
        extra_arguments: tuple[str, ...] = (),
        environment: Mapping[str, str] | None = None,
        with_log_file: bool = True,
    ) -> None:
        self.secret = secret if secret is not None else new_secret()
        self.port = port if port is not None else free_port()
        self._module = module
        self._extra = extra_arguments
        self._environment = dict(environment or {})
        self._with_log_file = with_log_file
        # Everything the process may write lives under one root, outside the repository.
        self._root = tempfile.TemporaryDirectory(prefix="ls-run-")
        self.temp_dir = Path(self._root.name, "tmp")
        self.work_dir = Path(self._root.name, "cwd")
        self.log_file = Path(self._root.name, "service.log")
        self.temp_dir.mkdir()
        self.work_dir.mkdir()
        self.stdout_text = ""
        self.stderr_text = ""
        self._process: subprocess.Popen[bytes] | None = None
        self._threads: list[threading.Thread] = []
        self._chunks: dict[str, list[bytes]] = {"stdout": [], "stderr": []}

    @property
    def base_url(self) -> str:
        """The address the service listens on."""
        return f"http://127.0.0.1:{self.port}"

    def __enter__(self) -> "RunningService":
        self.start()
        return self

    def __exit__(
        self,
        kind: type[BaseException] | None,
        error: BaseException | None,
        trace: TracebackType | None,
    ) -> None:
        self.stop()

    def start(self) -> None:
        """Launch the process, hand it the secret and wait until it answers."""
        environment = {
            **os.environ,
            "TMP": str(self.temp_dir),
            "TEMP": str(self.temp_dir),
            "TMPDIR": str(self.temp_dir),
            "PYTHONDONTWRITEBYTECODE": "1",
            "PYTHONUNBUFFERED": "1",
            "PYTHONUTF8": "1",
            **self._environment,
        }
        arguments = [sys.executable, "-m", self._module, "--port", str(self.port), *self._extra]
        if self._with_log_file:
            arguments += ["--log-file", str(self.log_file)]
        self._process = subprocess.Popen(  # noqa: S603 - fixed argument list, no shell
            arguments,
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            cwd=self.work_dir,
            env=environment,
        )
        assert self._process.stdin is not None
        self._process.stdin.write(self.secret.encode("ascii") + b"\n")
        self._process.stdin.close()
        for name, stream in (("stdout", self._process.stdout), ("stderr", self._process.stderr)):
            thread = threading.Thread(target=self._drain, args=(name, stream), daemon=True)
            thread.start()
            self._threads.append(thread)
        self._wait_until_up()

    def _drain(self, name: str, stream: object) -> None:
        read = stream.read1 if hasattr(stream, "read1") else stream.read  # type: ignore[attr-defined]
        while chunk := read(4096):
            self._chunks[name].append(chunk)

    def _wait_until_up(self) -> None:
        assert self._process is not None
        deadline = time.monotonic() + 30
        while time.monotonic() < deadline:
            if self._process.poll() is not None:
                self._collect()
                raise RuntimeError(f"service exited with {self._process.returncode}: {self.stderr_text}")
            try:
                reply = httpx.get(
                    f"{self.base_url}/health", headers={SECRET_HEADER: self.secret}, timeout=1.0
                )
                if reply.status_code == 200:
                    return
            except httpx.TransportError:
                pass
            time.sleep(0.05)
        self.stop()
        raise TimeoutError("service did not answer /health within 30 s")

    def client(self, secret: str | None = None) -> httpx.Client:
        """A client for the service; with the real secret unless another one is given."""
        headers = {SECRET_HEADER: secret if secret is not None else self.secret}
        return httpx.Client(base_url=self.base_url, headers=headers, timeout=10.0)

    def _collect(self) -> None:
        for thread in self._threads:
            thread.join(timeout=5)
        self.stdout_text = b"".join(self._chunks["stdout"]).decode("utf-8", "replace")
        self.stderr_text = b"".join(self._chunks["stderr"]).decode("utf-8", "replace")

    def stop(self) -> None:
        """End the process and keep what it wrote; the files stay until ``dispose``."""
        if self._process is not None and self._process.poll() is None:
            self._process.terminate()
            try:
                self._process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                self._process.kill()
                self._process.wait()
        self._collect()

    def dispose(self) -> None:
        """Delete the run's files."""
        self._root.cleanup()
