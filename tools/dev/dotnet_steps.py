"""The .NET steps of check.py: build of the reached projects, format of the files of the diff and
tests without a database. Each runs the `dotnet` of the machine without a resident server
(no MSBuild node reuse, no shared compiler), the way the CI does, so a finding here is one there."""

from __future__ import annotations

import re
from collections.abc import Iterable

from scope import Scope
from steps.common import Context, StepResult, failed, passed
from steps.test_core import WITHOUT_REAL_SERVICE

BUILD = "build"
FORMAT = "formato"
TESTS = "testes"
NO_SERVER = ("-nodeReuse:false", "-p:UseSharedCompilation=false", "-p:UseRazorBuildServer=false")
DIAGNOSTIC = re.compile(r": (error|warning) ")
FAILED_TEST = re.compile(r"^\s*failed\s+(.+)$", re.MULTILINE)
SHOWN = 8
LINE_WIDTH = 170


def project_file(name: str) -> str:
    """The path of a project under core/, from its name: tests have `Tests` in it."""
    folder = "tests" if ".Tests" in name else "src"
    return f"core/{folder}/{name}/{name}.csproj"


def lines_of(text: str, pattern: re.Pattern[str]) -> list[str]:
    """The distinct lines of ``text`` the pattern finds, cut to a width, the first ``SHOWN``."""
    seen: dict[str, None] = {}
    for line in text.splitlines():
        if pattern.search(line):
            seen.setdefault(line.strip()[:LINE_WIDTH])
    return list(seen)[:SHOWN]


def build(ctx: Context, scope: Scope) -> StepResult:
    """Builds in Release, with the analyzers as errors, each leaf of the reached projects."""
    if not scope.leaves:
        return passed(BUILD, "nenhum projeto .NET alcançado")
    for name in scope.leaves:
        command = ["dotnet", "build", project_file(name), "-c", "Release", "-m:2", *NO_SERVER]
        outcome = ctx.run([*command, "-nologo", "-clp:ErrorsOnly"], ctx.root)
        if not outcome.ok:
            found = lines_of(outcome.text, DIAGNOSTIC) or lines_of(outcome.text, re.compile("."))
            return failed(BUILD, f"{name} não compila:", *found)
    return passed(
        BUILD, f"{len(scope.leaves)} projetos construídos ({len(scope.reached)} alcançados)"
    )


def format_check(ctx: Context, scope: Scope) -> StepResult:
    """`dotnet format --verify-no-changes` over the C# files of the diff, project by project."""
    if not scope.files:
        return passed(FORMAT, "nenhum arquivo C# no diff")
    for name, files in scope.files.items():
        command = ["dotnet", "format", project_file(name), "--verify-no-changes", "--no-restore"]
        outcome = ctx.run([*command, "--include", *files], ctx.root)
        if not outcome.ok:
            found = lines_of(outcome.text, DIAGNOSTIC) or lines_of(outcome.text, re.compile("."))
            return failed(
                FORMAT, f"{name} fora do formato (`dotnet format core/Core.slnx`):", *found
            )
    count = sum(len(files) for files in scope.files.values())
    return passed(FORMAT, f"{count} arquivos C# conformes")


def tests(ctx: Context, scope: Scope) -> StepResult:
    """Runs, project by project and without rebuilding, the reached tests that need no database."""
    left = [f"fora: {line}" for line in scope.not_run]
    for name in scope.tests:
        command = ["dotnet", "test", "--project", project_file(name), "-c", "Release", "--no-build"]
        outcome = ctx.run([*command, *WITHOUT_REAL_SERVICE, "--no-progress"], ctx.root)
        if not outcome.ok:
            found = lines_of(outcome.text, FAILED_TEST) or lines_of(outcome.text, re.compile("."))
            return failed(TESTS, f"{name} reprovou:", *found, *left)
    return passed(TESTS, f"{len(scope.tests)} projetos de teste sem banco passaram", *left)


def names(projects: Iterable[str]) -> str:
    return ", ".join(projects) or "nenhum"
