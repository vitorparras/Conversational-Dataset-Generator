"""What the service needs before it serves: the port from the command line, the secret from stdin.

The messages here are for whoever operates the service, in Portuguese. There is no option for the
host: the service binds the loopback interface and nothing can move it.
"""

import re
from collections.abc import Sequence
from typing import BinaryIO, Final

MIN_SECRET_BYTES: Final = 16
MAX_SECRET_BYTES: Final = 1024
MIN_PORT: Final = 1
MAX_PORT: Final = 65535
PORT_OPTION: Final = "--port"
PORT_DIGITS: Final = re.compile(r"[0-9]{1,5}")
PORT_ARGUMENTS: Final = 2
EXIT_USAGE: Final = 2
USAGE: Final = (
    f"argumentos inválidos; uso: python -m language_service {PORT_OPTION} PORTA "
    f"({MIN_PORT} a {MAX_PORT})"
)
MISSING_SECRET_MESSAGE: Final = (
    "a entrada padrão não trouxe o segredo de inicialização "
    f"(uma linha de {MIN_SECRET_BYTES} a {MAX_SECRET_BYTES} bytes)"
)


def parse_port(argv: Sequence[str]) -> int | None:
    """The port when ``argv`` is exactly ``--port N`` with N in range; None for anything else."""
    if len(argv) != PORT_ARGUMENTS or argv[0] != PORT_OPTION or not PORT_DIGITS.fullmatch(argv[1]):
        return None
    port = int(argv[1])
    if MIN_PORT <= port <= MAX_PORT:
        return port
    return None


def read_secret(stream: BinaryIO) -> bytes | None:
    """The first line of ``stream`` without its line break; None when it is not a usable secret."""
    line = stream.readline(MAX_SECRET_BYTES + 2).rstrip(b"\r\n")
    if MIN_SECRET_BYTES <= len(line) <= MAX_SECRET_BYTES:
        return line
    return None
