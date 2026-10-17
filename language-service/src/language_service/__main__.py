"""Entry point: ``python -m language_service --port N``, secret on standard input."""

import sys
from collections.abc import Sequence

from language_service.app import create_app
from language_service.server import serve
from language_service.startup import (
    EXIT_USAGE,
    MISSING_SECRET_MESSAGE,
    USAGE,
    parse_port,
    read_secret,
)


def main(argv: Sequence[str] | None = None) -> int:
    port = parse_port(sys.argv[1:] if argv is None else argv)
    if port is None:
        print(USAGE, file=sys.stderr)
        return EXIT_USAGE
    secret = read_secret(sys.stdin.buffer)
    if secret is None:
        print(MISSING_SECRET_MESSAGE, file=sys.stderr)
        return EXIT_USAGE
    serve(create_app(secret), port, sys.stdin.buffer)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
