"""Tests for the systems: the interpreter path of the contract and the host that builds each."""

from __future__ import annotations

import unittest
from unittest import mock

import systems


class SystemsTests(unittest.TestCase):
    def test_linux_keeps_the_interpreter_under_bin(self) -> None:
        self.assertEqual("python/bin/python3", systems.SYSTEMS["linux-x64"].interpreter)

    def test_windows_keeps_the_interpreter_at_the_root_of_python(self) -> None:
        self.assertEqual("python/python.exe", systems.SYSTEMS["win-x64"].interpreter)

    def test_the_host_is_the_system_of_its_platform_and_machine(self) -> None:
        with (
            mock.patch.object(systems.sys, "platform", "linux"),
            mock.patch.object(systems.platform, "machine", return_value="x86_64"),
        ):
            self.assertTrue(systems.LINUX_X64.is_host())
            self.assertFalse(systems.WINDOWS_X64.is_host())

    def test_another_machine_is_no_host(self) -> None:
        with (
            mock.patch.object(systems.sys, "platform", "linux"),
            mock.patch.object(systems.platform, "machine", return_value="aarch64"),
        ):
            self.assertFalse(systems.LINUX_X64.is_host())
