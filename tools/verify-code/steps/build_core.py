"""Restores the core in locked mode and builds it; an outdated lock file is a failure."""

from __future__ import annotations

from steps.common import Context, StepResult, failed, passed, tail

NAME = "build_core"
SOLUTION = "core/Core.slnx"


def run(ctx: Context) -> StepResult:
    if not (ctx.root / SOLUTION).is_file():
        return failed(NAME, f"{SOLUTION} não existe: não há o que construir")
    restore = ctx.run(["dotnet", "restore", SOLUTION, "--locked-mode"], ctx.root)
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
