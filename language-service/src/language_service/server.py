"""Serving on the loopback interface, for as long as the process that started us lives."""

import json
import socket
import sys
import threading
from collections.abc import Callable
from dataclasses import dataclass
from typing import BinaryIO, Final, TextIO

import uvicorn
from starlette.types import ASGIApp

from language_service.logging_ import install_root_handler
from language_service.startup import BIND_FAILED_MESSAGE, EXIT_BIND

LOOPBACK_HOST: Final = "127.0.0.1"
WATCH_CHUNK_BYTES: Final = 1024
LISTEN_BACKLOG: Final = 128
LISTENING_EVENT: Final = "listening"


@dataclass(frozen=True)
class Streams:
    """The three standard streams of the process, passed in so that a test can stand in for them."""

    stdin: BinaryIO
    out: TextIO
    errors: TextIO

    @classmethod
    def of_process(cls) -> "Streams":
        """The streams of this process."""
        return cls(sys.stdin.buffer, sys.stdout, sys.stderr)


def build_config(app: ASGIApp, port: int) -> uvicorn.Config:
    """The server settings: the host is the constant, and the access log is off."""
    return uvicorn.Config(
        app,
        host=LOOPBACK_HOST,
        port=port,
        log_config=None,
        access_log=False,
        lifespan="off",
    )


def listen(port: int) -> socket.socket:
    """A socket already listening on the loopback interface; port 0 lets the system choose.

    The socket is opened here and handed to the server as it is, so the port has an owner from the
    moment it is chosen: there is no instant at which another process could take it. No other
    socket may share it: ``SO_EXCLUSIVEADDRUSE`` on Windows, where ``SO_REUSEADDR`` would let one,
    and the plain ``SO_REUSEADDR`` elsewhere, where it only skips the wait after a previous owner.
    """
    listener = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    try:
        if sys.platform == "win32":
            listener.setsockopt(socket.SOL_SOCKET, socket.SO_EXCLUSIVEADDRUSE, 1)
        else:
            listener.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        listener.bind((LOOPBACK_HOST, port))
        listener.listen(LISTEN_BACKLOG)
    except OSError:
        listener.close()
        raise
    return listener


def announce(listener: socket.socket, out: TextIO) -> None:
    """Tells the process that started us, by one line on ``out``, the port ``listener`` holds.

    The line is written after the socket listens and before the server serves, so whoever reads it
    can connect at once: the system queues the connection until the server accepts it.
    """
    port = int(listener.getsockname()[1])
    out.write(json.dumps({"event": LISTENING_EVENT, "port": port}) + "\n")
    out.flush()


def watch_stream(stream: BinaryIO, on_end: Callable[[], None]) -> threading.Thread:
    """Calls ``on_end`` when ``stream`` ends, which only happens when the core is gone."""

    def wait() -> None:
        while stream.read(WATCH_CHUNK_BYTES):
            pass
        on_end()

    thread = threading.Thread(target=wait, name="parent-watch", daemon=True)
    thread.start()
    return thread


def serve(app: ASGIApp, listener: socket.socket, stream: BinaryIO) -> None:
    """Serves ``app`` on ``listener`` until ``stream`` ends or the server is stopped.

    The whole log of the process goes to standard error through the fixed-field handler.
    """
    install_root_handler(sys.stderr)
    server = uvicorn.Server(build_config(app, int(listener.getsockname()[1])))

    def stop() -> None:
        server.should_exit = True

    watch_stream(stream, stop)
    server.run(sockets=[listener])


def run(app: ASGIApp, port: int, streams: Streams) -> int:
    """Opens the port, announces it and serves until the input ends; the exit code.

    A port that cannot be opened is told to the error stream before any thread starts, so the
    process leaves at once and cleanly, with nothing announced.
    """
    try:
        listener = listen(port)
    except OSError:
        print(BIND_FAILED_MESSAGE, file=streams.errors)
        return EXIT_BIND
    with listener:
        announce(listener, streams.out)
        serve(app, listener, streams.stdin)
    return 0
