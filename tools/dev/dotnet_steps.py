"""The .NET steps of check.py, with no resident build server, as the CI runs them."""

from __future__ import annotations

import re

from scope import Scope
from steps import mutation_core
from steps.common import Context, StepResult, failed, passed
from steps.test_core import WITHOUT_REAL_SERVICE

BUILD, FORMAT, TESTS = "build", "formato", "testes"
NO_SERVER = ("-nodeReuse:false", "-p:UseSharedCompilation=false", "-p:UseRazorBuildServer=false")
DIAGNOSTIC = re.compile(r": (error|warning) ")
FAILED_TEST = re.compile(r"^\s*failed\s+(.+)$", re.MULTILINE)
ANSI = re.compile(r"\x1b\[[0-9;]*[A-Za-z]")
SOLUTION = "core/Core.slnx"
MAX_INCLUDED = 150  # more paths than this would not fit the command line of Windows
SHOWN, LINE_WIDTH = 8, 170  # lines kept of a failure, and their width


def project_file(name: str) -> str:
    tree = mutation_core.TESTS if ".Tests" in name else mutation_core.SOURCES
    return f"{tree}/{name}/{name}.csproj"


def findings(text: str, pattern: re.Pattern[str]) -> list[str]:
    """The first distinct lines ``pattern`` finds or, when none, the first lines: never silent."""
    lines = [line.strip()[:LINE_WIDTH] for line in ANSI.sub("", text).splitlines() if line.strip()]
    hits = [line for line in lines if pattern.search(line)] or lines
    return list(dict.fromkeys(hits))[:SHOWN]


def build(ctx: Context, scope: Scope) -> StepResult:
    if not scope.leaves:
        return passed(BUILD, "nenhum projeto .NET alcançado")
    for name in scope.leaves:
        command = ["dotnet", "build", project_file(name), "-c", "Release", "-m:2", *NO_SERVER]
        outcome = ctx.run([*command, "-nologo", "-clp:ErrorsOnly"], ctx.root)
        if not outcome.ok:
            return failed(BUILD, f"{name} não compila:", *findings(outcome.text, DIAGNOSTIC))
    return passed(BUILD, f"{len(scope.leaves)} construídos ({len(scope.reached)} alcançados)")


def format_check(ctx: Context, scope: Scope) -> StepResult:
    if not scope.files:
        return passed(FORMAT, "nenhum arquivo C# no diff")
    command = ["dotnet", "format", SOLUTION, "--verify-no-changes", "--no-restore"]
    only = ["--include", *scope.files] if len(scope.files) <= MAX_INCLUDED else []
    outcome = ctx.run([*command, *only], ctx.root)
    if not outcome.ok:
        return failed(FORMAT, "rode `dotnet format`:", *findings(outcome.text, DIAGNOSTIC))
    return passed(FORMAT, f"{len(scope.files)} arquivos C# conformes")


def tests(ctx: Context, scope: Scope) -> StepResult:
    left = [f"fora: {line}" for line in scope.not_run]
    for name in scope.tests:
        project = project_file(name)
        command = ["dotnet", "test", "--project", project, "-c", "Release", "--no-build"]
        outcome = ctx.run([*command, *WITHOUT_REAL_SERVICE, "--no-progress"], ctx.root)
        if not outcome.ok:
            return failed(TESTS, f"{name} reprovou:", *findings(outcome.text, FAILED_TEST), *left)
    return passed(TESTS, f"{len(scope.tests)} projetos de teste sem banco passaram", *left)
