"""Runs only the core tests that start the real language service (trait `Category=RealService`).

`test_core` leaves them out, so it passes without `uv`; this step is where they run, with `uv`
installed. A test project the filter leaves empty is no failure, but the step as a whole must run
at least one test, and every test that was skipped is listed with the reason it gave."""

from __future__ import annotations

from steps.build_core import SOLUTION
from steps.common import Context, StepResult, failed, passed, reset_directory, tail
from steps.test_core import REAL_SERVICE_TRAIT, describe_skips, read_trx

NAME = "test_real_service"
RESULTS = "real-service"
# a test project the filter leaves without a test exits with 8; the step as a whole must run one
EMPTY_PROJECT_EXIT = "8"


def run(ctx: Context) -> StepResult:
    if not (ctx.root / SOLUTION).is_file():
        return failed(NAME, f"{SOLUTION} não existe: não há suíte para rodar")
    results = ctx.out_dir / RESULTS
    reset_directory(results)
    command = [
        "dotnet",
        "test",
        SOLUTION,
        "--no-build",
        "--report-xunit-trx",
        "--filter-trait",
        REAL_SERVICE_TRAIT,
        "--ignore-exit-code",
        EMPTY_PROJECT_EXIT,
        "--results-directory",
        str(results),
    ]
    outcome = ctx.run(command, ctx.root)
    runs = tuple(read_trx(trx) for trx in sorted(results.glob("*.trx")))
    skips = tuple(describe_skips(run) for run in runs if run.skipped > 0)
    executed, failures = sum(r.executed for r in runs), sum(r.failed for r in runs)
    if executed == 0:
        return failed(
            NAME,
            f"nenhum teste {REAL_SERVICE_TRAIT} executou: o passo não passa vazio",
            *skips,
            *tail(outcome.text),
        )
    if failures > 0:
        return failed(
            NAME, f"{failures} teste(s) do serviço real falharam", *skips, *tail(outcome.text)
        )
    if not outcome.ok:
        return failed(
            NAME, "`dotnet test` terminou com erro sem teste reprovado", *skips, *tail(outcome.text)
        )
    return passed(NAME, f"{executed} testes do serviço real executados", *skips)
