"""The server binds the loopback interface by construction, and stops when standard input ends."""

import inspect
import os
import threading
from collections.abc import Iterator
from typing import BinaryIO

import pytest

from language_service.app import create_app
from language_service.server import LOOPBACK_HOST, build_config, serve, watch_stream
from support.synthetic import SECRET

PORT = 8123


@pytest.fixture
def pipe() -> Iterator[tuple[BinaryIO, int]]:
    """The read end of a pipe, as the service sees standard input, and the write descriptor."""
    read_end, write_end = os.pipe()
    with os.fdopen(read_end, "rb") as stream:
        yield stream, write_end


def test_loopback_host_is_the_local_interface() -> None:
    assert LOOPBACK_HOST == "127.0.0.1"


def test_server_config_binds_the_loopback_host() -> None:
    config = build_config(create_app(SECRET), PORT)

    assert (config.host, config.port) == (LOOPBACK_HOST, PORT)


def test_server_config_has_no_access_log() -> None:
    config = build_config(create_app(SECRET), PORT)

    assert config.access_log is False


def test_nothing_that_builds_the_server_takes_a_host() -> None:
    parameters = {*inspect.signature(build_config).parameters, *inspect.signature(serve).parameters}

    assert "host" not in parameters


def test_watch_waits_while_the_stream_is_open(pipe: tuple[BinaryIO, int]) -> None:
    stream, write_end = pipe
    ended = threading.Event()

    watch_stream(stream, ended.set)

    assert not ended.wait(timeout=0.2)
    os.close(write_end)


def test_watch_signals_when_the_stream_ends(pipe: tuple[BinaryIO, int]) -> None:
    stream, write_end = pipe
    ended = threading.Event()
    watch_stream(stream, ended.set)

    os.close(write_end)

    assert ended.wait(timeout=5)
