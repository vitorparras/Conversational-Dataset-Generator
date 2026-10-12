"""Build the language service from the frozen lock file."""

from __future__ import annotations

from steps.common import Context, StepResult, run_command

NAME = "build_service"
GROUP = "service"
DESCRIPTION = "instala (travado) e compila o serviço de linguagem"


def run(ctx: Context) -> StepResult:
    result = StepResult()
    project = ctx.root / "language-service"
    if not (project / "pyproject.toml").is_file():
        result.fail("language-service/pyproject.toml não existe: não há o que construir")
        return result
    if run_command(["uv", "sync", "--frozen", "--directory", str(project)], ctx.root, result):
        run_command(
            ["uv", "run", "--frozen", "--directory", str(project), "python", "-m", "compileall", "-q", "src"],
            ctx.root,
            result,
        )
    if result.ok:
        result.note("serviço instalado do uv.lock e compilado")
    return result
