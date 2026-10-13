"""Runs the unit tests of tools/ with the standard library runner; zero tests found is a failure."""

from __future__ import annotations

import re
import sys

from steps.common import Context, StepResult, failed, passed, tail

NAME = "test_tools"
TOOLS = "tools"


def run(ctx: Context) -> StepResult:
    if not (ctx.root / TOOLS).is_dir():
        return failed(NAME, f"{TOOLS}/ não existe: não há suíte para rodar")
    command = [sys.executable, "-m", "unittest", "discover", "-s", TOOLS, "-p", "test_*.py"]
    outcome = ctx.run(command, ctx.root)
    ran = re.search(r"^Ran (\d+) tests?", outcome.text, re.MULTILINE)
    if ran is None or int(ran.group(1)) == 0:
        return failed(NAME, f"nenhum teste encontrado em {TOOLS}/: o passo não passa vazio",
                      *tail(outcome.text))
    if not outcome.ok:
        return failed(NAME, f"testes de {TOOLS}/ falharam", *tail(outcome.text))
    return passed(NAME, f"{ran.group(1)} testes de {TOOLS}/ executados")
