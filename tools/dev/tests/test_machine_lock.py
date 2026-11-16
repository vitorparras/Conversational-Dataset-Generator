"""The lock of the machine: one build at a time, a dead holder's lock is taken over."""

from __future__ import annotations

import os
import tempfile
import time
import unittest
from pathlib import Path
from unittest import mock

import machine_lock

RELEASED_AFTER = 3


class MachineLockTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.path = Path(self._tmp.name) / "maquina.lock"

    def test_the_lock_is_a_directory_while_held_and_gone_after(self) -> None:
        with machine_lock.machine_lock(self.path, print):
            self.assertTrue(self.path.is_dir())

        self.assertFalse(self.path.exists())

    def test_the_lock_is_released_when_the_body_fails(self) -> None:
        with self.assertRaises(RuntimeError), machine_lock.machine_lock(self.path, print):
            raise RuntimeError

        self.assertFalse(self.path.exists())

    def test_a_held_lock_makes_the_next_wait_and_say_so_once(self) -> None:
        self.path.mkdir()
        said: list[str] = []
        waits: list[float] = []

        def sleep(seconds: float) -> None:
            waits.append(seconds)
            if len(waits) == RELEASED_AFTER:
                self.path.rmdir()

        with machine_lock.machine_lock(self.path, said.append, sleep):
            pass

        self.assertEqual(RELEASED_AFTER, len(waits))
        self.assertEqual(1, len(said))

    def test_a_lock_nobody_touched_for_forty_minutes_is_taken_over(self) -> None:
        self.path.mkdir()
        old = self.path.stat().st_mtime - machine_lock.STALE_SECONDS - 60
        os.utime(self.path, (old, old))

        self.assertTrue(machine_lock.try_take(self.path, old + machine_lock.STALE_SECONDS + 120))

    def test_a_lock_the_holder_renewed_is_not_taken_over(self) -> None:
        self.path.mkdir()
        old = self.path.stat().st_mtime - machine_lock.STALE_SECONDS - 60
        os.utime(self.path, (old, old))
        looked_at = old + machine_lock.STALE_SECONDS + 120
        real_stat = Path.stat
        looks: list[Path] = []

        def renewed_after_the_first_look(
            path: Path, *args: object, **kwargs: object
        ) -> os.stat_result:
            result = real_stat(path, *args, **kwargs)  # type: ignore[arg-type]
            if not looks:
                os.utime(path)
            looks.append(path)
            return result

        with mock.patch.object(Path, "stat", renewed_after_the_first_look):
            self.assertFalse(machine_lock.try_take(self.path, looked_at))

        self.assertTrue(self.path.is_dir())

    def test_the_holder_keeps_the_lock_young_while_it_works(self) -> None:
        old = time.time() - 1000
        with (
            mock.patch.object(machine_lock, "BEAT_SECONDS", 0.05),
            machine_lock.machine_lock(self.path, print),
        ):
            os.utime(self.path, (old, old))
            time.sleep(0.4)

            self.assertGreater(self.path.stat().st_mtime, old + 900)

    def test_a_lock_that_never_frees_up_ends_in_a_timeout_naming_the_path(self) -> None:
        self.path.mkdir()

        with (
            self.assertRaisesRegex(TimeoutError, "maquina.lock"),
            machine_lock.machine_lock(self.path, print, lambda seconds: None, limit=20),
        ):
            pass

        self.assertTrue(self.path.is_dir())

    def test_a_lock_removed_by_somebody_else_does_not_break_the_release(self) -> None:
        with machine_lock.machine_lock(self.path, print):
            self.path.rmdir()

    def test_a_lock_another_holder_took_over_is_left_alone(self) -> None:
        other = self.path.with_name("other.lock")
        with machine_lock.machine_lock(self.path, print):
            other.mkdir()
            self.path.rename(self.path.with_name("old.lock"))
            other.rename(self.path)

        self.assertTrue(self.path.is_dir())

    def test_without_an_override_the_lock_lives_in_the_temp_dir(self) -> None:
        os.environ.pop(machine_lock.OVERRIDE, None)

        self.assertEqual(Path(tempfile.gettempdir()) / "maquina.lock", machine_lock.lock_path())

    def test_the_override_names_the_lock(self) -> None:
        os.environ[machine_lock.OVERRIDE] = str(self.path)
        self.addCleanup(os.environ.pop, machine_lock.OVERRIDE)

        self.assertEqual(self.path, machine_lock.lock_path())
