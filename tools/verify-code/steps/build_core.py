"""Build the core from a locked restore, with warnings as errors."""

from __future__ import annotations

from steps.common import Context, StepResult, run_command

NAME = "build_core"
GROUP = "core"
DESCRIPTION = "restaura (travado) e constrói o núcleo .NET sem avisos"


def run(ctx: Context) -> StepResult:
    result = StepResult()
    solution = ctx.root / "core" / "Core.slnx"
    if not solution.is_file():
        result.fail("core/Core.slnx não existe: não há o que construir")
        return result
    if run_command(["dotnet", "restore", str(solution), "--locked-mode"], ctx.root, result):
        run_command(["dotnet", "build", str(solution), "--no-restore", "-warnaserror"], ctx.root, result)
    if result.ok:
        result.note("núcleo construído a partir de restauração travada, sem avisos")
    return result
