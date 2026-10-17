"""Serving on the loopback interface, for as long as the process that started us lives."""

import sys
import threading
from collections.abc import Callable
from typing import BinaryIO, Final

import uvicorn
from starlette.types import ASGIApp

from language_service.logging_ import install_root_handler

LOOPBACK_HOST: Final = "127.0.0.1"
WATCH_CHUNK_BYTES: Final = 1024


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


def watch_stream(stream: BinaryIO, on_end: Callable[[], None]) -> threading.Thread:
    """Calls ``on_end`` when ``stream`` ends, which only happens when the core is gone."""

    def wait() -> None:
        while stream.read(WATCH_CHUNK_BYTES):
            pass
        on_end()

    thread = threading.Thread(target=wait, name="parent-watch", daemon=True)
    thread.start()
    return thread


def serve(app: ASGIApp, port: int, stream: BinaryIO) -> None:
    """Serves ``app`` on the loopback interface until ``stream`` ends or the server is stopped.

    The whole log of the process goes to standard error through the fixed-field handler.
    """
    install_root_handler(sys.stderr)
    server = uvicorn.Server(build_config(app, port))

    def stop() -> None:
        server.should_exit = True

    watch_stream(stream, stop)
    server.run()
