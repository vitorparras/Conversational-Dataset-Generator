"""The documentation step of check.py: the verifier of the documents, run as the CI runs it."""

from __future__ import annotations

import sys

from steps.common import Context, StepResult, failed, passed, tail

NAME = "docs"
SCRIPT = "tools/verify-docs/verify.py"


def run(ctx: Context) -> StepResult:
    outcome = ctx.run([sys.executable, SCRIPT], ctx.root)
    if outcome.ok:
        return passed(NAME, "documentação conforme")
    return failed(NAME, "verify-docs reprovou:", *tail(outcome.text))
