"""Shared fixture of the mutation tests: a synthetic repository and a runner that plays both tools.

The runner writes what Stryker.NET and mutmut would leave on disk, from fixtures given per test;
no tool runs. Every name is synthetic.
"""

from __future__ import annotations

import tempfile
import unittest
from collections.abc import Sequence
from dataclasses import dataclass, field
from pathlib import Path

from steps import areas_doc, mutation
from steps.common import OUTPUT_DIRECTORY, CommandOutcome, Context, StepResult

from . import support
from .mutation_samples import DOCUMENT, SHOWN, SOURCE

STRYKER_REPORT = "reports/mutation-report.json"


@dataclass
class ToolRunner:
    """Plays `dotnet tool restore`, Stryker and mutmut: writes their output, records each call."""

    stryker: str = ""
    metas: dict[str, str] = field(default_factory=dict[str, str])
    failing: tuple[str, ...] = ()
    commands: list[list[str]] = field(default_factory=list[list[str]])
    folders: list[Path] = field(default_factory=list[Path])

    def __call__(self, command: Sequence[str], cwd: Path) -> CommandOutcome:
        self.commands.append(list(command))
        self.folders.append(cwd)
        if any(word in command for word in self.failing):
            return CommandOutcome(False, f"{command[0]}: ZQX-MARKER-FAIL")
        if "stryker" in command and self.stryker:
            output = Path(command[command.index("--output") + 1])
            support.write(output, STRYKER_REPORT, self.stryker)
        if "run" in command and "mutmut" in command:
            for file, text in self.metas.items():
                support.write(cwd / "mutants", f"{file}.meta", text)
        return CommandOutcome(True, SHOWN if "show" in command else "")

    def called(self, word: str) -> list[list[str]]:
        return [command for command in self.commands if word in command]


class MutationFixture(unittest.TestCase):
    """A temporary repository with one critical core module and two service ones."""

    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name).resolve()
        support.write(self.root, areas_doc.DOCUMENT, DOCUMENT)
        reference = (
            '<ProjectReference Include="..\\..\\src\\Core.Platform\\Core.Platform.csproj" />'
        )
        support.write(self.root, "core/src/Core.Platform/Core.Platform.csproj", "<Project />\n")
        support.write(self.root, "core/src/Core.Platform/Logging/Formatter.cs", "class F {}\n")
        support.write(
            self.root, "core/tests/Core.Platform.Tests/Core.Platform.Tests.csproj", reference
        )
        support.write(self.root, "core/tests/Core.Host.Tests/Core.Host.Tests.csproj", "<Project />")
        service = "language-service/src/language_service"
        support.write(self.root, f"{service}/auth.py", SOURCE)
        support.write(self.root, f"{service}/logging_/__init__.py", "")
        support.write(self.root, "language-service/tests/test_auth.py", "")
        support.write(self.root, "language-service/pyproject.toml", "[project]\n")

    @property
    def logging_file(self) -> str:
        return str(self.root / "core/src/Core.Platform/Logging/Formatter.cs")

    def run_step(self, runner: ToolRunner) -> StepResult:
        return mutation.run(Context(self.root, self.root / OUTPUT_DIRECTORY, runner))
