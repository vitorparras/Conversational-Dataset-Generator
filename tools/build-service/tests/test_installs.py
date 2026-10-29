"""Tests for what is taken out of the installed packages: launchers and files of a local install."""

from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

import installs

LAUNCHER = "../../Scripts/serve.exe"
RECORD_HEAD = "pkg/__init__.py,sha256=aaa,3\n"


class InstallsTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.python = Path(self._tmp.name) / "python"
        self.site = self.python / "Lib" / "site-packages"
        self.scripts = self.python / "Scripts"
        self.scripts.mkdir(parents=True)
        (self.site / "pkg").mkdir(parents=True)
        (self.site / "pkg" / "__init__.py").write_text("X = 1\n", encoding="utf-8")

    def install(self, name: str, launcher: str = LAUNCHER, url: str | None = None) -> Path:
        """A package with a launcher, and with a direct URL when ``url`` is given."""
        metadata = self.site / f"{name}.dist-info"
        metadata.mkdir()
        (self.scripts / "serve.exe").write_bytes(b"MZ C:/build/python.exe")
        rows = [RECORD_HEAD, f"{launcher},sha256=bbb,9\n", f"{name}.dist-info/RECORD,,\n"]
        if url is not None:
            (metadata / "direct_url.json").write_text(json.dumps({"url": url}), encoding="utf-8")
            (metadata / "uv_cache.json").write_text("{}", encoding="utf-8")
            rows.insert(2, f"{name}.dist-info/direct_url.json,sha256=ccc,5\n")
        (metadata / "RECORD").write_text("".join(rows), encoding="utf-8")
        return metadata

    def record(self, metadata: Path) -> list[str]:
        return (metadata / "RECORD").read_text(encoding="utf-8").splitlines()

    def test_the_launcher_goes_away_with_its_line_in_the_record(self) -> None:
        metadata = self.install("pkg-1.0")

        installs.scrub(self.site, self.scripts, frozenset())

        self.assertFalse((self.scripts / "serve.exe").exists())
        self.assertEqual([RECORD_HEAD.strip(), "pkg-1.0.dist-info/RECORD,,"], self.record(metadata))

    def test_a_launcher_written_with_backslashes_in_the_record_goes_away_too(self) -> None:
        self.install("pkg-1.0", launcher=LAUNCHER.replace("/", "\\"))

        installs.scrub(self.site, self.scripts, frozenset())

        self.assertFalse((self.scripts / "serve.exe").exists())

    def test_what_the_package_installs_beside_its_code_stays(self) -> None:
        self.install("pkg-1.0")

        installs.scrub(self.site, self.scripts, frozenset())

        self.assertTrue((self.site / "pkg" / "__init__.py").is_file())

    def test_a_package_the_interpreter_brought_is_left_as_it_is(self) -> None:
        metadata = self.install("pip-1.0")
        before = (metadata / "RECORD").read_bytes()

        removed = installs.scrub(self.site, self.scripts, installs.installed(self.site))

        self.assertEqual(0, removed)
        self.assertEqual(before, (metadata / "RECORD").read_bytes())
        self.assertTrue((self.scripts / "serve.exe").exists())

    def test_a_package_installed_from_a_folder_loses_the_files_that_name_the_folder(self) -> None:
        metadata = self.install("svc-1.0", url="file:///tmp/build/language-service")

        installs.scrub(self.site, self.scripts, frozenset())

        self.assertEqual(
            [], [n for n in ("direct_url.json", "uv_cache.json") if (metadata / n).exists()]
        )
        self.assertNotIn("direct_url", "\n".join(self.record(metadata)))

    def test_a_package_installed_from_a_url_keeps_where_it_came_from(self) -> None:
        metadata = self.install("model-1.0", url="https://example.org/model.whl")

        installs.scrub(self.site, self.scripts, frozenset())

        self.assertTrue((metadata / "direct_url.json").is_file())

    def test_two_installs_that_differ_only_in_the_build_path_end_up_equal(self) -> None:
        first = self.install("svc-1.0", url="file:///tmp/one/language-service")
        installs.scrub(self.site, self.scripts, frozenset())
        record_first = (first / "RECORD").read_bytes()
        (first / "RECORD").unlink()
        first.rmdir()
        second = self.install("svc-1.0", url="file:///tmp/two/language-service")
        (self.scripts / "serve.exe").write_bytes(b"MZ D:/other/python.exe")

        installs.scrub(self.site, self.scripts, frozenset())

        self.assertEqual(record_first, (second / "RECORD").read_bytes())
