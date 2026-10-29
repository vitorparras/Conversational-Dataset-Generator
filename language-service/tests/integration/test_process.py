"""The real process: it serves once it has the secret, and leaves when standard input ends."""

import socket
import subprocess
from collections.abc import Iterator

import pytest

from language_service.startup import (
    BIND_FAILED_MESSAGE,
    EXIT_BIND,
    EXIT_USAGE,
    MISSING_SECRET_MESSAGE,
    USAGE,
)
from support import process
from support.process import HEALTHY, STOP_TIMEOUT_SECONDS

pytestmark = pytest.mark.integration


@pytest.fixture
def running() -> Iterator[tuple[int, subprocess.Popen[bytes]]]:
    """The service started on a free port with the secret written, stopped after the test."""
    port = process.free_port()
    service = process.start(port)
    yield port, service
    process.stop(service)


def test_service_answers_health_once_it_has_the_secret(
    running: tuple[int, subprocess.Popen[bytes]],
) -> None:
    port, service = running

    assert process.wait_for_health(port, service) == HEALTHY


def test_service_exits_cleanly_when_standard_input_ends(
    running: tuple[int, subprocess.Popen[bytes]],
) -> None:
    port, service = running
    process.wait_for_health(port, service)

    exit = process.close_stdin_and_wait(service)

    assert (exit.code, exit.seconds < STOP_TIMEOUT_SECONDS) == (0, True)


def test_service_without_port_exits_with_usage() -> None:
    exit = process.run()

    assert (exit.code, exit.stderr.strip()) == (EXIT_USAGE, USAGE)


def test_service_without_secret_exits_with_usage() -> None:
    exit = process.run("--port", str(process.free_port()))

    assert (exit.code, exit.stderr.strip()) == (EXIT_USAGE, MISSING_SECRET_MESSAGE)


def test_service_started_on_port_zero_announces_the_port_it_holds() -> None:
    service = process.start(0)
    try:
        port = process.announced_port(service)

        assert port is not None
        assert process.wait_for_health(port, service) == HEALTHY
    finally:
        process.stop(service)


def test_service_on_a_port_someone_holds_leaves_with_the_bind_code_and_announces_nothing() -> None:
    with socket.socket() as holder:
        holder.bind((process.LOOPBACK, 0))
        holder.listen()
        service = process.start(holder.getsockname()[1])

        output = process.finish(service)

    assert (output.code, output.stdout, output.stderr.decode("utf-8").strip()) == (
        EXIT_BIND,
        b"",
        BIND_FAILED_MESSAGE,
    )
