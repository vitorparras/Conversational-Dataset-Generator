"""The lock that lets one build run on the machine at a time, shared with `build-com-trava.sh`.

The lock is a directory: `mkdir` either creates it or fails, atomically, on every system. One older
than ``STALE_SECONDS`` belongs to a process that died and is taken over.
"""

from __future__ import annotations

import os
import tempfile
import time
from collections.abc import Callable, Generator
from contextlib import contextmanager
from pathlib import Path

LOCK_NAME = "maquina.lock"
SHARED_FOLDER = Path("C:/tmp")
OVERRIDE = "CDG_MACHINE_LOCK"
STALE_SECONDS = 40 * 60
POLL_SECONDS = 5.0


def lock_path() -> Path:
    """`CDG_MACHINE_LOCK`, else `C:/tmp/maquina.lock` if that folder exists, else the temp dir."""
    if os.environ.get(OVERRIDE):
        return Path(os.environ[OVERRIDE])
    base = SHARED_FOLDER if SHARED_FOLDER.is_dir() else Path(tempfile.gettempdir())
    return base / LOCK_NAME


def is_stale(path: Path, now: float) -> bool:
    """True when the lock exists and nobody touched it for ``STALE_SECONDS``."""
    try:
        return now - path.stat().st_mtime > STALE_SECONDS
    except OSError:
        return False


def try_take(path: Path, now: float) -> bool:
    """Creates the lock; a stale one is removed first. False when somebody else holds it."""
    if is_stale(path, now):
        path.rmdir()
    try:
        path.mkdir()
    except FileExistsError:
        return False
    return True


@contextmanager
def machine_lock(
    path: Path, say: Callable[[str], None], sleep: Callable[[float], None] = time.sleep
) -> Generator[None]:
    """Holds the lock for the body; waits, saying so once, while another build holds it."""
    announced = False
    while not try_take(path, time.time()):
        if not announced:
            say(f"outro build usa a máquina ({path}); aguardando a vez")
            announced = True
        sleep(POLL_SECONDS)
    try:
        yield
    finally:
        path.rmdir()
