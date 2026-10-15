"""The command line takes only a port, and the secret is one line of standard input."""

from io import BytesIO

import pytest

from language_service.startup import EXIT_USAGE, parse_port, read_secret
from support.synthetic import SECRET

PORT = 8123


def test_port_is_read_from_the_command_line() -> None:
    assert parse_port(["--port", str(PORT)]) == PORT


def test_host_option_is_refused() -> None:
    with pytest.raises(SystemExit) as exit_info:
        parse_port(["--port", "8123", "--host", "0.0.0.0"])

    assert exit_info.value.code == EXIT_USAGE


def test_missing_port_is_refused() -> None:
    with pytest.raises(SystemExit) as exit_info:
        parse_port([])

    assert exit_info.value.code == EXIT_USAGE


def test_secret_is_the_first_line_without_its_break() -> None:
    stream = BytesIO(SECRET + b"\r\nnever read\n")

    assert read_secret(stream) == SECRET


def test_secret_is_read_once_leaving_the_rest_of_the_stream() -> None:
    stream = BytesIO(SECRET + b"\nrest\n")

    read_secret(stream)

    assert stream.read() == b"rest\n"


def test_empty_stream_yields_no_secret() -> None:
    assert read_secret(BytesIO(b"")) is None


def test_too_short_line_yields_no_secret() -> None:
    assert read_secret(BytesIO(b"short\n")) is None
