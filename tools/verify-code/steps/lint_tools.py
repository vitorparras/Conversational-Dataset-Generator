"""Checks tools/ with ruff format, ruff check and strict pyright; the first finding fails the step.

The three checks run through the interpreter that runs the verifier, so the versions pinned in
lint-requirements.txt are the ones used. ``lint`` is shared with the step that checks the service.
"""

from __future__ import annotations

import sys
from dataclasses import dataclass

from steps.common import Context, StepResult, failed, passed, tail

NAME = "lint_tools"
TOOLS = "tools"
REQUIREMENTS = "tools/verify-code/lint-requirements.txt"
RUFF_CONFIG = ("--config", "ruff.toml")


@dataclass(frozen=True)
class Check:
    """One linter run over a folder through the interpreter running the verifier, and its remedy."""

    module: str
    arguments: tuple[str, ...]
    remedy: str

    def command(self, target: str) -> list[str]:
        return [sys.executable, "-m", self.module, *self.arguments, target]

    def label(self, target: str) -> str:
        return " ".join((self.module, *self.arguments, target))


CHECKS: tuple[Check, ...] = (
    Check("ruff", ("format", "--check", *RUFF_CONFIG), "rode `ruff format {target}`"),
    Check(
        "ruff",
        ("check", *RUFF_CONFIG),
        "corrija cada achado pelo código; `ruff.toml` é a fonte de estilo",
    ),
    Check(
        "pyright", (), "anote os tipos até o modo estrito de `pyrightconfig.json` não acusar nada"
    ),
)


def lint(ctx: Context, name: str, target: str) -> StepResult:
    """Runs the three checks over ``target`` in order, stopping at the first that reprove."""
    if not (ctx.root / target).is_dir():
        return failed(name, f"{target}/ não existe: não há o que verificar")
    for check in CHECKS:
        outcome = ctx.run(check.command(target), ctx.root)
        if not outcome.ok:
            return failed(
                name,
                f"`{check.label(target)}` reprovou: {check.remedy.format(target=target)}",
                f"ferramenta ausente? instale com `pip install -r {REQUIREMENTS}`",
                *tail(outcome.text),
            )
    return passed(name, f"ruff format, ruff check e pyright estrito sem achado em {target}/")


def run(ctx: Context) -> StepResult:
    return lint(ctx, NAME, TOOLS)
