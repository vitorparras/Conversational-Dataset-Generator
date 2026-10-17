"""Entry point: ``python -m language_service --port N``, secret on standard input."""

import sys
from collections.abc import Sequence

from language_service.app import create_app
from language_service.server import serve
from language_service.startup import EXIT_USAGE, read_startup


def main(argv: Sequence[str] | None = None) -> int:
    startup = read_startup(sys.argv[1:] if argv is None else argv, sys.stdin.buffer, sys.stderr)
    if startup is None:
        return EXIT_USAGE
    serve(create_app(startup.secret), startup.port, sys.stdin.buffer)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
