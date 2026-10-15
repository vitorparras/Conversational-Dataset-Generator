"""What the service needs before it serves: the port from the command line, the secret from stdin.

The messages here are for whoever operates the service, in Portuguese. There is no option for the
host: the service binds the loopback interface and nothing can move it.
"""

import argparse
from collections.abc import Sequence
from typing import BinaryIO, Final, NoReturn

MIN_SECRET_BYTES: Final = 16
MAX_SECRET_BYTES: Final = 1024
EXIT_USAGE: Final = 2
USAGE: Final = "uso: python -m language_service --port PORTA"
MISSING_SECRET_MESSAGE: Final = (
    "a entrada padrão não trouxe o segredo de inicialização "
    f"(uma linha de {MIN_SECRET_BYTES} a {MAX_SECRET_BYTES} bytes)"
)


class OperatorParser(argparse.ArgumentParser):
    """An argument parser that refuses in the operator's language and accepts only the port."""

    def __init__(self) -> None:
        super().__init__(prog="language_service", add_help=False, allow_abbrev=False)
        self.add_argument(
            "--port", type=int, required=True, choices=range(1, 65536), metavar="PORTA"
        )

    def error(self, message: str) -> NoReturn:
        self.exit(EXIT_USAGE, f"argumentos inválidos; {USAGE}\n")


def parse_port(argv: Sequence[str]) -> int:
    """The port given on the command line; anything else on it ends the process."""
    arguments = OperatorParser().parse_args(argv)
    port: int = arguments.port
    return port


def read_secret(stream: BinaryIO) -> bytes | None:
    """The first line of ``stream`` without its line break; None when it is not a usable secret."""
    line = stream.readline(MAX_SECRET_BYTES + 2).rstrip(b"\r\n")
    if MIN_SECRET_BYTES <= len(line) <= MAX_SECRET_BYTES:
        return line
    return None
