"""Shared helpers of the tests of tools/verify-code. Synthetic data only.

Not a test module (its name does not start with test_): each test module loads it by path, so the
tests run the same whether discovery starts at tools/ or at this folder.
"""

from __future__ import annotations

import sys
import tempfile
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parent
TOOL = HERE.parent
FIXTURES = HERE / "fixtures"

if str(TOOL) not in sys.path:
    sys.path.insert(0, str(TOOL))

from steps.common import FLOORS_DOCUMENT, Context  # noqa: E402

#: Source files whose existence the fixture reports refer to.
SOURCE_FILES = (
    "core/src/Core.Platform/Logging/Writer.cs",
    "core/src/Core.Platform/Configuration/Loader.cs",
    "core/src/Core.Host/Program.cs",
    "language-service/src/language_service/auth.py",
    "language-service/src/language_service/logging_.py",
    "tools/verify-code/verify.py",
)


class RepositoryTestCase(unittest.TestCase):
    """A throwaway repository in a temporary directory, with the fixture reports in its output folder."""

    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name).resolve()
        self.out_dir = self.root / ".verify-code"
        for relative in SOURCE_FILES:
            self.write(relative, "// synthetic\n")
        self.write(FLOORS_DOCUMENT, (FIXTURES / "floors-document.md").read_text(encoding="utf-8"))
        self.place_report("cobertura-a.xml", "core/a/coverage.cobertura.xml")
        self.place_report("cobertura-b.xml", "core/b/coverage.cobertura.xml")
        self.place_report("coverage-py.xml", "service/coverage.xml")
        self.place_report("tools-coverage.xml", "tools/coverage.xml")

    def context(self, *, ci: bool = False, environment: dict[str, str] | None = None) -> Context:
        return Context(root=self.root, out_dir=self.out_dir, ci=ci, environment=environment or {})

    def write(self, relative: str, content: str) -> None:
        target = self.root / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(content, encoding="utf-8", newline="\n")

    def place_report(self, fixture: str, relative: str) -> None:
        """Copy a fixture report into the output folder, pointing its sources at the temporary root."""
        text = (FIXTURES / fixture).read_text(encoding="utf-8").replace("@ROOT@", self.root.as_posix())
        target = self.out_dir / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(text, encoding="utf-8", newline="\n")

    def edit_document(self, old: str, new: str) -> None:
        """Change the floors document of the temporary repository."""
        path = self.root / FLOORS_DOCUMENT
        text = path.read_text(encoding="utf-8")
        self.assertIn(old, text)
        path.write_text(text.replace(old, new), encoding="utf-8", newline="\n")
