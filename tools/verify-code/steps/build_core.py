"""Restores the core in locked mode, resolving again, and builds it; a stale lock is a failure."""

from __future__ import annotations

from steps.common import Context, StepResult, failed, passed, tail

NAME = "build_core"
SOLUTION = "core/Core.slnx"
# `--force` resolves again even when an earlier restore left `obj/` up to date: without it the
# locked restore is a no-op and never compares the lock file with the project.
RESTORE = ["dotnet", "restore", SOLUTION, "--locked-mode", "--force"]


def run(ctx: Context) -> StepResult:
    if not (ctx.root / SOLUTION).is_file():
        return failed(NAME, f"{SOLUTION} não existe: não há o que construir")
    restore = ctx.run(RESTORE, ctx.root)
    if not restore.ok:
        return failed(
            NAME,
            "restauração travada falhou: lock desatualizado ou pacote indisponível",
            *tail(restore.text),
        )
    build = ctx.run(["dotnet", "build", SOLUTION, "--no-restore"], ctx.root)
    if not build.ok:
        return failed(NAME, "construção do núcleo falhou", *tail(build.text))
    return passed(NAME, "núcleo restaurado em modo travado e construído sem avisos")
