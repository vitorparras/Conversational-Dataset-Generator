"""The real process: it serves once it has the secret, and leaves when standard input ends."""

import subprocess
from collections.abc import Iterator

import pytest

from language_service.startup import EXIT_USAGE, MISSING_SECRET_MESSAGE, USAGE
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
