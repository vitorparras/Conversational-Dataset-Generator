"""Entry point: ``python -m language_service --port N`` (0: the system chooses), secret on stdin."""

import sys
from collections.abc import Sequence

from language_service.app import create_app
from language_service.server import Streams, run
from language_service.startup import EXIT_USAGE, read_startup


def main(argv: Sequence[str] | None = None) -> int:
    startup = read_startup(sys.argv[1:] if argv is None else argv, sys.stdin.buffer, sys.stderr)
    if startup is None:
        return EXIT_USAGE
    return run(create_app(startup.secret), startup.port, Streams.of_process())


if __name__ == "__main__":
    raise SystemExit(main())
