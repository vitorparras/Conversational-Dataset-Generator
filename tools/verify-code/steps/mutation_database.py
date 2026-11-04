"""The database container a Stryker run reuses, so a restarted server does not start another."""

from __future__ import annotations

import contextlib
import os
from collections.abc import Generator
from pathlib import Path

from steps.common import Context

# Stryker kills and restarts the test server after a mutant times out; a test project that starts
# its own database container would start it again each time. The variable asks that project for
# one container and image, named after the token, that outlive the server; the step removes both.
REUSE_VARIABLE = "CDG_TEST_DATABASE_REUSE"
DATABASE_PACKAGE = "Testcontainers"
DOCKER = "docker"
# The names the test project gives the container and the image of a token (DatabaseReuse.cs).
CONTAINER_NAME = "cdg-test-database-{token}"
IMAGE_NAME = "cdg-test-database:{token}"


def starts_database(tests: tuple[Path, ...]) -> bool:
    """Whether a test project starts a database container of its own (references Testcontainers)."""
    return any(DATABASE_PACKAGE in path.read_text(encoding="utf-8") for path in tests)


@contextlib.contextmanager
def reusing(token: str | None) -> Generator[None]:
    """The environment of the Stryker run: the reuse token set for its test servers, then back."""
    if token is None:
        yield
        return
    before = os.environ.get(REUSE_VARIABLE)
    os.environ[REUSE_VARIABLE] = token
    try:
        yield
    finally:
        if before is None:
            del os.environ[REUSE_VARIABLE]
        else:
            os.environ[REUSE_VARIABLE] = before


def discard_database(ctx: Context, token: str) -> None:
    """Removes the container and the image a run with ``token`` left; neither is there when no
    test ran, so a failure to remove is no finding."""
    for command in (
        [DOCKER, "rm", "--force", "--volumes", CONTAINER_NAME.format(token=token)],
        [DOCKER, "rmi", "--force", IMAGE_NAME.format(token=token)],
    ):
        ctx.run(command, ctx.root)
