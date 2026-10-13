"""Checks the Python of tools/: ruff format, ruff check and pyright strict, none with a finding."""

from __future__ import annotations

import sys
from dataclasses import dataclass

from steps.common import Context, StepResult, failed, passed, tail

NAME = "lint_tools"
TOOLS = "tools"
REQUIREMENTS = "tools/verify-code/lint-requirements.txt"


@dataclass(frozen=True)
class Check:
    """One linter run over tools/ through the interpreter running the verifier, and its remedy."""

    module: str
    arguments: tuple[str, ...]
    remedy: str

    @property
    def command(self) -> list[str]:
        return [sys.executable, "-m", self.module, *self.arguments, TOOLS]

    @property
    def label(self) -> str:
        return " ".join((self.module, *self.arguments, TOOLS))


CHECKS: tuple[Check, ...] = (
    Check("ruff", ("format", "--check"), "rode `ruff format tools`"),
    Check("ruff", ("check",), "corrija cada achado pelo código; `ruff.toml` é a fonte de estilo"),
    Check(
        "pyright", (), "anote os tipos até o modo estrito de `pyrightconfig.json` não acusar nada"
    ),
)


def run(ctx: Context) -> StepResult:
    if not (ctx.root / TOOLS).is_dir():
        return failed(NAME, f"{TOOLS}/ não existe: não há o que verificar")
    for check in CHECKS:
        outcome = ctx.run(check.command, ctx.root)
        if not outcome.ok:
            return failed(
                NAME,
                f"`{check.label}` reprovou: {check.remedy}",
                f"ferramenta ausente? instale com `pip install -r {REQUIREMENTS}`",
                *tail(outcome.text),
            )
    return passed(NAME, f"ruff format, ruff check e pyright estrito sem achado em {TOOLS}/")
