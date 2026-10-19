"""Shared helpers for the tests of tools/verify-code: module loading, a fake runner, a git fixture.

Every repository the tests build is synthetic and lives in a temporary directory.
"""

from __future__ import annotations

import importlib.util
import subprocess
import sys
from collections.abc import Mapping, Sequence
from dataclasses import dataclass, field
from pathlib import Path
from types import ModuleType

from steps.common import OUTPUT_DIRECTORY, CommandOutcome, Context, run_process

VERIFY_CODE = Path(__file__).resolve().parents[1]
SUCCESS = CommandOutcome(True, "")

SOLUTION_WITH_TESTS = (
    "<Solution>\n"
    '  <Project Path="src/Core.Host/Core.Host.csproj" />\n'
    '  <Project Path="tests/Alpha.Tests/Alpha.Tests.csproj" />\n'
    '  <Project Path="tests/Beta.Tests/Beta.Tests.csproj" />\n'
    "</Solution>\n"
)

PYPROJECT_WITH_TESTS = (
    "[project]\n"
    'name = "language-service"\n'
    'version = "0.1.0"\n'
    "[tool.coverage.run]\n"
    'source = ["language_service"]\n'
)

FIXTURE_IDENTITY = (
    "-c",
    "user.name=ZQX-MARKER-7731",
    "-c",
    "user.email=zqx@example.invalid",
    "-c",
    "commit.gpgsign=false",
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
    """Answers every command with one outcome after writing the given files; records them all."""

    outcome: CommandOutcome = SUCCESS
    files: Mapping[str, str] = field(default_factory=dict[str, str])
    write_into: Path | None = None
    commands: list[list[str]] = field(default_factory=list[list[str]])

    def __call__(self, command: Sequence[str], cwd: Path) -> CommandOutcome:
        self.commands.append(list(command))
        for name, content in self.files.items():
            assert self.write_into is not None
            (self.write_into / name).write_text(content, encoding="utf-8")
        return self.outcome


def context(root: Path, run: FakeRunner | None = None, base: str | None = None) -> Context:
    """A context on ``root``; without a fake, commands really run."""
    return Context(root, root / OUTPUT_DIRECTORY, run if run is not None else run_process, base)


def write(root: Path, relative: str, content: str) -> None:
    target = root / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(content, encoding="utf-8", newline="\n")


def trx(project: str, executed: int, failed: int, skipped: str = "") -> str:
    """A TRX report reduced to what the step reads: counters, one assembly, one skip reason."""
    skip = (
        '  <Results><UnitTestResult testName="s" outcome="NotExecuted">'
        f"<Output><StdOut>{skipped}</StdOut></Output></UnitTestResult></Results>\n"
        if skipped
        else ""
    )
    return (
        '<?xml version="1.0" encoding="utf-8"?>\n'
        '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">\n'
        f'  <ResultSummary><Counters total="{executed}" executed="{executed}" '
        f'passed="{executed - failed}" failed="{failed}" notExecuted="{1 if skipped else 0}" />'
        "</ResultSummary>\n"
        f'  <TestDefinitions><UnitTest name="t" id="1"><TestMethod codeBase="bin/{project}.dll" '
        'className="c" name="t" /></UnitTest></TestDefinitions>\n'
        f"{skip}"
        "</TestRun>\n"
    )


def junit(tests: int, failures: int, skipped: int = 0) -> str:
    """A JUnit report reduced to what the step reads: one suite and its counters."""
    return (
        '<?xml version="1.0" encoding="utf-8"?>\n'
        f'<testsuites><testsuite name="pytest" errors="0" failures="{failures}" '
        f'skipped="{skipped}" tests="{tests}" time="0.1" /></testsuites>\n'
    )


def git(repo: Path, *args: str) -> None:
    subprocess.run(["git", *FIXTURE_IDENTITY, *args], cwd=repo, check=True, capture_output=True)


def git_repository(path: Path) -> Path:
    """A repository with one commit on `main` and a `change` branch checked out on top of it."""
    git(path, "init", "-q", "-b", "main")
    write(path, "README.md", "base\n")
    git(path, "add", ".")
    git(path, "commit", "-q", "-m", "base")
    git(path, "checkout", "-q", "-b", "change")
    return path


def numbered_files(prefix: str, count: int) -> list[str]:
    return [f"{prefix}{index}.cs" for index in range(count)]


def commit_files(repo: Path, names: Sequence[str], lines_each: int) -> None:
    """Writes every named file with ``lines_each`` lines and commits them on the current branch."""
    for name in names:
        write(repo, name, "x\n" * lines_each)
    git(repo, "add", ".")
    git(repo, "commit", "-q", "-m", "change")
