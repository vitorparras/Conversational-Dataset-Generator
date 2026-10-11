"""Entry point: ``python -m language_service --port N``.

The core writes the secret to standard input, one line, and closes it. It is read once, here, and
only its digest is kept. The host is a constant: no argument, setting or environment variable
moves the service off the loopback interface (decision 0011).
"""

import argparse
import logging
import sys
from collections.abc import Sequence
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
    try:
        secret = read_secret(stdin if stdin is not None else sys.stdin.buffer)
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

    _silence_server_loggers()
    app = create_app(verifier=verifier, recognizer=recognizer, log=log)
    log.event(step="startup", outcome="ok", model_version=recognizer.model_version)
    try:
        uvicorn.Server(build_config(app, arguments.port)).run()
    finally:
        if log_file is not None:
            log_file.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
