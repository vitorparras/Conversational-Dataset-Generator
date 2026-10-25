"""Where the log of the process can be read: every handler of every logger, by logger name."""

from __future__ import annotations

import logging
from typing import Final, TextIO

from language_service.logging_ import FixedFieldHandler

ROOT_NAME: Final = logging.getLogger().name
TEST_RUNNER_MODULE: Final = "_pytest."


def handlers_by_logger() -> tuple[tuple[str, logging.Handler], ...]:
    """Each handler of the root logger or of any created logger, but the test runner's own.

    The runner attaches its capture handlers to the root logger while a test runs; they are not
    part of the service, which never has them.
    """
    created = (
        logger
        for logger in logging.root.manager.loggerDict.values()
        if isinstance(logger, logging.Logger)
    )
    return tuple(
        (logger.name, handler)
        for logger in (logging.getLogger(), *created)
        for handler in logger.handlers
        if not type(handler).__module__.startswith(TEST_RUNNER_MODULE)
    )


def stray_destinations(stream: TextIO) -> tuple[str, ...]:
    """The handlers that are not the one destination: the fixed-field handler on ``stream``.

    Each is named by its logger and its class, so a failure says where the extra destination is.
    """
    return tuple(
        f"{name}: {type(handler).__name__}"
        for name, handler in handlers_by_logger()
        if not _is_the_destination(name, handler, stream)
    )


def _is_the_destination(name: str, handler: logging.Handler, stream: TextIO) -> bool:
    return (
        name == ROOT_NAME
        and type(handler) is FixedFieldHandler
        and getattr(handler, "stream", None) is stream
    )
