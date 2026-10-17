"""Syncs the language service's environment from its lock file; an outdated lock is a failure."""

from __future__ import annotations

from steps.common import Context, StepResult, failed, passed, tail

NAME = "build_service"
SERVICE = "language-service"
PROJECT = f"{SERVICE}/pyproject.toml"
REQUIREMENTS = "tools/verify-code/service-requirements.txt"
UV = ("uv",)
LOCKED = ("--locked", "--directory", SERVICE)


def run(ctx: Context) -> StepResult:
    if not (ctx.root / PROJECT).is_file():
        return failed(NAME, f"{PROJECT} não existe: não há o que construir")
    sync = ctx.run([*UV, "sync", *LOCKED], ctx.root)
    if not sync.ok:
        return failed(
            NAME,
            "sincronização travada falhou: lock desatualizado ou pacote indisponível",
            f"ferramenta ausente? instale com `pip install -r {REQUIREMENTS}`",
            *tail(sync.text),
        )
    return passed(NAME, f"ambiente de {SERVICE}/ sincronizado a partir de uv.lock, sem alterá-lo")
