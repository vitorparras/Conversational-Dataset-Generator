"""Entry point: ``python -m language_service --port N``.

The core writes the secret to standard input, one line, and keeps the stream open for as long as it
lives; the end of the stream means the core is gone, and the service stops. The secret is read
once, here, and only its digest is kept. The host is a constant: no argument, setting or environment variable
moves the service off the loopback interface (decision 0011).
"""

import argparse
import logging
import sys
import threading
from collections.abc import Callable, Sequence
from typing import BinaryIO, Final, TextIO

import uvicorn
from starlette.types import ASGIApp

from language_service.app import create_app
from language_service.auth import SecretVerifier
from language_service.logging_ import ServiceLog
from language_service.recognizers.empty import EmptyRecognizer

LOOPBACK_HOST: Final = "127.0.0.1"
MIN_SECRET_BYTES: Final = 16
MAX_SECRET_BYTES: Final = 1024
EXIT_USAGE: Final = 2


class SecretNotProvidedError(Exception):
    """Standard input held no usable secret."""


def parse_arguments(argv: Sequence[str] | None = None) -> argparse.Namespace:
    """Read the command line. There is a port and an optional log file, and no host."""
    parser = argparse.ArgumentParser(prog="language_service", allow_abbrev=False)
    parser.add_argument("--port", type=int, required=True, choices=range(1, 65536), metavar="PORT")
    parser.add_argument("--log-file", default=None, metavar="PATH")
    return parser.parse_args(argv)


def read_secret(stream: BinaryIO) -> bytes:
    """Read the secret: the first line of ``stream``, without its line break."""
    line = stream.readline(MAX_SECRET_BYTES + 3).rstrip(b"\r\n")
    if not MIN_SECRET_BYTES <= len(line) <= MAX_SECRET_BYTES:
        raise SecretNotProvidedError("standard input did not carry a secret of the expected size")
    return line


def watch_parent(stream: BinaryIO, on_parent_gone: Callable[[], None]) -> threading.Thread:
    """Call ``on_parent_gone`` when ``stream`` reaches its end, which is when the core is gone.

    The core keeps the write end of this stream open for as long as it lives, and the system closes
    it when the core dies, however it dies. Nothing is ever written after the secret, so the only
    thing a read can return is the end.
    """

    def wait() -> None:
        try:
            while stream.read(1024):
                pass
        except (OSError, ValueError):
            pass
        on_parent_gone()

    thread = threading.Thread(target=wait, name="parent-watch", daemon=True)
    thread.start()
    return thread


def build_config(app: ASGIApp, port: int) -> uvicorn.Config:
    """The server settings. The host is the constant; the access log is off."""
    return uvicorn.Config(
        app,
        host=LOOPBACK_HOST,
        port=port,
        log_config=None,
        access_log=False,
        server_header=False,
        date_header=False,
        ws="none",
        lifespan="off",
        loop="asyncio",
        http="h11",
    )


def disable_core_dumps() -> bool:
    """Ask the system not to write a memory dump if the process dies; False where it cannot.

    A dump would hold the text being analysed, on disk and outside the cipher (policy 003).
    """
    try:
        import resource  # noqa: PLC0415 - only exists on POSIX
    except ImportError:
        return False
    resource.setrlimit(resource.RLIMIT_CORE, (0, 0))
    return True


def _silence_server_loggers() -> None:
    # The server's own loggers can print exception text and request lines; the service log is the
    # only output, so they get no handler and no propagation.
    for name in ("uvicorn", "uvicorn.error", "uvicorn.access", "asyncio"):
        target = logging.getLogger(name)
        target.handlers.clear()
        target.addHandler(logging.NullHandler())
        target.propagate = False
        target.setLevel(logging.CRITICAL + 1)


def main(
    argv: Sequence[str] | None = None,
    stdin: BinaryIO | None = None,
    stdout: TextIO | None = None,
) -> int:
    """Start the service and block until it stops; return the exit code."""
    arguments = parse_arguments(argv)
    source = stdin if stdin is not None else sys.stdin.buffer
    try:
        secret = read_secret(source)
    except SecretNotProvidedError as error:
        sys.stderr.write(f"{error}\n")
        return EXIT_USAGE
    verifier = SecretVerifier(secret)
    del secret

    sinks: list[TextIO] = [stdout if stdout is not None else sys.stdout]
    log_file: TextIO | None = None
    if arguments.log_file is not None:
        log_file = open(arguments.log_file, "a", encoding="utf-8")  # noqa: SIM115 - lives with the process
        sinks.append(log_file)
    log = ServiceLog(sinks)
    recognizer = EmptyRecognizer()

    disable_core_dumps()
    _silence_server_loggers()
    app = create_app(verifier=verifier, recognizer=recognizer, log=log)
    log.event(step="startup", outcome="ok", model_version=recognizer.model_version)
    try:
        server = uvicorn.Server(build_config(app, arguments.port))
        watch_parent(source, lambda: setattr(server, "should_exit", True))
        server.run()
    finally:
        if log_file is not None:
            log_file.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
