"""The command line takes only a port, and the secret is one line of standard input."""

from io import BytesIO

from language_service.startup import parse_port, read_secret
from support.synthetic import SECRET

PORT = 8123


def test_port_is_read_from_the_command_line() -> None:
    assert parse_port(["--port", str(PORT)]) == PORT


def test_host_option_is_refused() -> None:
    assert parse_port(["--port", str(PORT), "--host", "0.0.0.0"]) is None


def test_missing_port_is_refused() -> None:
    assert parse_port([]) is None


def test_port_that_is_not_a_number_is_refused() -> None:
    assert parse_port(["--port", "http"]) is None


def test_port_above_the_range_is_refused() -> None:
    assert parse_port(["--port", "65536"]) is None


def test_port_zero_is_refused() -> None:
    assert parse_port(["--port", "0"]) is None


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
