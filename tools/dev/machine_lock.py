"""One build at a time on the machine: the directory lock of `build-com-trava.sh`.

`mkdir` creates it or fails, atomically. One older than ``STALE_SECONDS`` is a dead process's.
"""

from __future__ import annotations

import os
import tempfile
import time
from collections.abc import Callable, Generator
from contextlib import contextmanager, suppress
from pathlib import Path

LOCK_NAME = "maquina.lock"
OVERRIDE = "CDG_MACHINE_LOCK"
STALE_SECONDS = 40 * 60
POLL_SECONDS = 5.0


def lock_path() -> Path:
    """`CDG_MACHINE_LOCK`, else `C:/tmp/maquina.lock` (the temp dir when `C:/tmp` is missing)."""
    shared = Path("C:/tmp")
    default = (shared if shared.is_dir() else Path(tempfile.gettempdir())) / LOCK_NAME
    return Path(os.environ.get(OVERRIDE) or default)


def try_take(path: Path, now: float) -> bool:
    """Creates the lock, first removing a stale one; False when somebody else holds it."""
    with suppress(OSError):
        if now - path.stat().st_mtime > STALE_SECONDS:
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
    if not try_take(path, time.time()):
        say(f"outro build usa a máquina ({path}); aguardando a vez")
        while not try_take(path, time.time()):
            sleep(POLL_SECONDS)
    try:
        yield
    finally:
        path.rmdir()
