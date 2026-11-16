"""One build at a time on the machine, by a lock directory (`mkdir` is atomic).

The holder touches it every ``BEAT_SECONDS``; one untouched for ``STALE_SECONDS`` is a dead one's.
The holder's mark lives beside the directory (never inside it, so a script that only knows
`mkdir`/`rmdir` keeps working), and a holder releases only the lock that still carries its mark.
`CDG_MACHINE_LOCK` points the lock elsewhere, to share it with another tool."""

from __future__ import annotations

import os
import tempfile
import threading
import time
import uuid
from collections.abc import Callable, Generator
from contextlib import contextmanager, suppress
from pathlib import Path

OVERRIDE = "CDG_MACHINE_LOCK"
STALE_SECONDS = 40 * 60
BEAT_SECONDS = 60.0
POLL_SECONDS = 5.0
WAIT_SECONDS = 60 * 60


def lock_path() -> Path:
    return Path(os.environ.get(OVERRIDE) or Path(tempfile.gettempdir()) / "maquina.lock")


def try_take(path: Path, now: float) -> bool:
    """Creates the lock after taking a stale one by rename, which only one contender wins."""
    grave = path.with_name(f"{path.name}.{os.getpid()}")
    with suppress(OSError):
        if now - path.stat().st_mtime > STALE_SECONDS:
            path.rename(grave)
            if now - grave.stat().st_mtime > STALE_SECONDS:
                grave.rmdir()
            else:  # renewed in between: it was alive, give it back
                grave.rename(path)
    try:
        path.mkdir()
        return True
    except FileExistsError:
        return False


def mark_file(path: Path) -> Path:
    return path.with_name(f"{path.name}.dono")


def claim(path: Path) -> str:
    """Writes the holder's mark: a token of this run and the identity of the lock directory."""
    mark = f"{uuid.uuid4().hex} {path.stat().st_ino}"
    mark_file(path).write_text(mark, encoding="utf-8")
    return mark


def release(path: Path, mark: str) -> None:
    """Removes the lock only if it is still the one ``mark`` was written for."""
    with suppress(OSError):
        _, identity = mark.split()
        if (
            mark_file(path).read_text(encoding="utf-8") == mark
            and str(path.stat().st_ino) == identity
        ):
            path.rmdir()
            mark_file(path).unlink()


def beat(path: Path, stop: threading.Event) -> None:
    while not stop.wait(BEAT_SECONDS):
        with suppress(OSError):
            os.utime(path)


@contextmanager
def machine_lock(
    path: Path,
    say: Callable[[str], None],
    sleep: Callable[[float], None] = time.sleep,
    limit: float = WAIT_SECONDS,
) -> Generator[None]:
    """Holds the lock for the block; waits ``limit`` seconds for it, then raises TimeoutError."""
    waited = 0.0
    while not try_take(path, time.time()):
        if waited == 0.0:
            say(f"outro build usa a máquina ({path}); aguardando a vez")
        if waited >= limit:
            raise TimeoutError(f"a máquina seguiu ocupada por {limit / 60:.0f} min ({path})")
        sleep(POLL_SECONDS)
        waited += POLL_SECONDS
    stop = threading.Event()
    mark = claim(path)
    threading.Thread(target=beat, args=(path, stop), daemon=True).start()
    try:
        yield
    finally:
        stop.set()
        release(path, mark)
