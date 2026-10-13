"""Shared helpers for the tests of tools/verify-code: module loading, a fake runner, fixtures."""

from __future__ import annotations

import importlib.util
import sys
from collections.abc import Mapping, Sequence
from dataclasses import dataclass, field
from pathlib import Path
from types import ModuleType

VERIFY_CODE = Path(__file__).resolve().parents[1]
if str(VERIFY_CODE) not in sys.path:
    sys.path.insert(0, str(VERIFY_CODE))

from steps.common import OUTPUT_DIRECTORY, CommandOutcome, Context, run_process

SOLUTION_WITH_TESTS = (
    "<Solution>\n"
    '  <Project Path="src/Core.Host/Core.Host.csproj" />\n'
    '  <Project Path="tests/Alpha.Tests/Alpha.Tests.csproj" />\n'
    '  <Project Path="tests/Beta.Tests/Beta.Tests.csproj" />\n'
    "</Solution>\n"
)


def load_verify() -> ModuleType:
    """Imports verify.py under a name that does not collide with the verify-docs module."""
    spec = importlib.util.spec_from_file_location("verify_code", VERIFY_CODE / "verify.py")
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    sys.modules["verify_code"] = module
    spec.loader.exec_module(module)
    return module


@dataclass
class FakeRunner:
    """Answers every command with one outcome, writes the given files first, remembers the commands."""

    outcome: CommandOutcome = CommandOutcome(True, "")
    files: Mapping[str, str] = field(default_factory=dict[str, str])
    write_into: Path | None = None
    commands: list[list[str]] = field(default_factory=list[list[str]])

    def __call__(self, command: Sequence[str], cwd: Path) -> CommandOutcome:
        self.commands.append(list(command))
        for name, content in self.files.items():
            assert self.write_into is not None
            (self.write_into / name).write_text(content, encoding="utf-8")
        return self.outcome


def context(root: Path, run: FakeRunner | None = None) -> Context:
    """A context on ``root``; without a fake, commands really run."""
    return Context(root, root / OUTPUT_DIRECTORY, run if run is not None else run_process)


def write(root: Path, relative: str, content: str) -> None:
    target = root / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(content, encoding="utf-8", newline="\n")


def trx(project: str, executed: int, failed: int) -> str:
    """A TRX report reduced to what the step reads: the counters and one test method's assembly."""
    return (
        '<?xml version="1.0" encoding="utf-8"?>\n'
        '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">\n'
        f'  <ResultSummary><Counters total="{executed}" executed="{executed}" '
        f'passed="{executed - failed}" failed="{failed}" /></ResultSummary>\n'
        f'  <TestDefinitions><UnitTest name="t" id="1"><TestMethod codeBase="bin/{project}.dll" '
        'className="c" name="t" /></UnitTest></TestDefinitions>\n'
        "</TestRun>\n"
    )
