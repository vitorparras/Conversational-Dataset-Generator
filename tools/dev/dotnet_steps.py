"""The .NET steps of check.py: build, format and tests without a database, with the machine's
`dotnet` and no resident server (no node reuse, no shared compiler), as the CI runs them."""

from __future__ import annotations

import re

from scope import Scope
from steps import package, reach
from steps.common import Context, StepResult, failed, passed
from steps.test_core import WITHOUT_REAL_SERVICE

BUILD = "build"
FORMAT = "formato"
TESTS = "testes"
NO_SERVER = (*package.NO_SERVER, "-p:UseRazorBuildServer=false")
DIAGNOSTIC = re.compile(r": (error|warning) ")
FAILED_TEST = re.compile(r"^\s*failed\s+(.+)$", re.MULTILINE)
ANSI = re.compile(r"\x1b\[[0-9;]*[A-Za-z]")
SOLUTION = "core/Core.slnx"
MAX_INCLUDED = 150  # more paths than this would not fit the command line of Windows
SHOWN = 8
LINE_WIDTH = 170


def project_file(ctx: Context, name: str) -> str:
    return reach.project_files(ctx.root)[name].relative_to(ctx.root).as_posix()


def findings(text: str, pattern: re.Pattern[str]) -> list[str]:
    """The first distinct lines ``pattern`` finds or, when none, the first lines: never silent."""
    lines = [line.strip()[:LINE_WIDTH] for line in ANSI.sub("", text).splitlines() if line.strip()]
    hits = [line for line in lines if pattern.search(line)] or lines
    return list(dict.fromkeys(hits))[:SHOWN]


def build(ctx: Context, scope: Scope) -> StepResult:
    if not scope.leaves:
        return passed(BUILD, "nenhum projeto .NET alcançado")
    for name in scope.leaves:
        command = ["dotnet", "build", project_file(ctx, name), "-c", "Release", "-m:2", *NO_SERVER]
        outcome = ctx.run([*command, "-nologo", "-clp:ErrorsOnly"], ctx.root)
        if not outcome.ok:
            return failed(BUILD, f"{name} não compila:", *findings(outcome.text, DIAGNOSTIC))
    whole = ", o core inteiro: o diff chega a todos" if scope.everything else ""
    return passed(
        BUILD, f"{len(scope.leaves)} construídos ({len(scope.reached)} alcançados{whole})"
    )


def format_check(ctx: Context, scope: Scope) -> StepResult:
    if not scope.files:
        return passed(FORMAT, "nenhum arquivo C# no diff")
    command = ["dotnet", "format", SOLUTION, "--verify-no-changes", "--no-restore"]
    narrow = len(scope.files) <= MAX_INCLUDED
    only = ["--include", *scope.files] if narrow else []
    outcome = ctx.run([*command, *only], ctx.root)
    if not outcome.ok:
        return failed(
            FORMAT, f"rode `dotnet format {SOLUTION}`:", *findings(outcome.text, DIAGNOSTIC)
        )
    where = "" if narrow else f" (a solução inteira: mais de {MAX_INCLUDED} no diff)"
    return passed(FORMAT, f"{len(scope.files)} arquivos C# conformes{where}")


def tests(ctx: Context, scope: Scope) -> StepResult:
    left = [f"fora: {line}" for line in scope.not_run]
    for name in scope.tests:
        command = [
            "dotnet",
            "test",
            "--project",
            project_file(ctx, name),
            "-c",
            "Release",
            "--no-build",
        ]
        outcome = ctx.run([*command, *WITHOUT_REAL_SERVICE, "--no-progress"], ctx.root)
        if not outcome.ok:
            return failed(TESTS, f"{name} reprovou:", *findings(outcome.text, FAILED_TEST), *left)
    return passed(TESTS, f"{len(scope.tests)} projetos de teste sem banco passaram", *left)
