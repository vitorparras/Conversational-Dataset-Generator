"""Checks language-service/ with ruff format, ruff check and strict pyright over its own venv.

Before them, every `# noqa` of a ruff security rule (S) must carry its reason."""

from __future__ import annotations

from steps import noqa_reason
from steps.build_service import NAME as BUILD_STEP
from steps.build_service import SERVICE
from steps.common import Context, StepResult, failed
from steps.lint_tools import lint

NAME = "lint_service"
VENV = f"{SERVICE}/.venv"


def run(ctx: Context) -> StepResult:
    if (ctx.root / SERVICE).is_dir() and not (ctx.root / VENV).is_dir():
        return failed(NAME, f"{VENV}/ não existe: rode o passo {BUILD_STEP} antes, que o cria")
    unexplained = noqa_reason.unexplained(ctx.root, SERVICE)
    if unexplained:
        return failed(NAME, *unexplained, "escreva `# noqa: Sxxx <por que não é defeito>`")
    return lint(ctx, NAME, SERVICE)
