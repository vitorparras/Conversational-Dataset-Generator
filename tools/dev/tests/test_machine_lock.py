"""The lock of the machine: one build at a time, a dead holder's lock is taken over."""

from __future__ import annotations

import os
import tempfile
import unittest
from pathlib import Path

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

    def test_the_override_names_the_lock(self) -> None:
        os.environ[machine_lock.OVERRIDE] = str(self.path)
        self.addCleanup(os.environ.pop, machine_lock.OVERRIDE)

        self.assertEqual(self.path, machine_lock.lock_path())
