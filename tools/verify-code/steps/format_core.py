"""Checks that the core is formatted as `.editorconfig` demands; any change needed is a failure."""

from __future__ import annotations

from steps.build_core import SOLUTION
from steps.common import Context, StepResult, failed, passed, tail

NAME = "format_core"


def run(ctx: Context) -> StepResult:
    if not (ctx.root / SOLUTION).is_file():
        return failed(NAME, f"{SOLUTION} não existe: não há o que formatar")
    outcome = ctx.run(["dotnet", "format", SOLUTION, "--verify-no-changes", "--no-restore"], ctx.root)
    if not outcome.ok:
        return failed(NAME, "formatação diverge do .editorconfig: rode `dotnet format core/Core.slnx`",
                      *tail(outcome.text))
    return passed(NAME, "formatação do núcleo conforme o .editorconfig")
