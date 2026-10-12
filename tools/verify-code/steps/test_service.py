"""Run the service suites with coverage; a run that executes nothing is a failure."""

from __future__ import annotations

from steps.common import Context, StepResult, clean_directory, report_skips, run_command
from steps.reports import FAILED, SKIPPED, TestOutcome, read_junit

NAME = "test_service"
GROUP = "service"
DESCRIPTION = "roda as suítes do serviço (pytest) com cobertura coverage.py XML"

RESULTS = "service"
COVERAGE_FILE = "coverage.xml"
JUNIT_FILE = "junit.xml"


def evaluate(ctx: Context, outcomes: list[TestOutcome], result: StepResult) -> None:
    """Judge the outcomes: nothing executed, a failure or a skip."""
    executed = [o for o in outcomes if o.outcome != SKIPPED]
    if not executed:
        result.fail("nenhum teste executado no serviço: o passo não passa vazio")
        return
    result.note(f"{len(executed)} testes executados, {len(outcomes) - len(executed)} saltados")
    for outcome in outcomes:
        if outcome.outcome == FAILED:
            result.fail(f"teste falhou: {outcome.source}::{outcome.name}")
    report_skips(
        result, ctx, [f"{o.source}::{o.name}" for o in outcomes if o.outcome == SKIPPED], "serviço"
    )


def run(ctx: Context) -> StepResult:
    result = StepResult()
    project = ctx.root / "language-service"
    if not (project / "tests").is_dir():
        result.fail("language-service/tests não existe: não há suíte para rodar")
        return result
    results = ctx.out_dir / RESULTS
    clean_directory(results)
    coverage, junit = results / COVERAGE_FILE, results / JUNIT_FILE
    command = [
        "uv", "run", "--frozen", "--directory", str(project), "pytest", "-q", "-p", "no:cacheprovider",
        "--cov", f"--cov-report=xml:{coverage}", f"--junitxml={junit}",
    ]
    run_command(command, ctx.root, result)
    if not junit.is_file():
        result.fail("o pytest não escreveu o relatório JUnit")
        return result
    evaluate(ctx, read_junit(junit), result)
    if not coverage.is_file():
        result.fail("o pytest não escreveu o relatório de cobertura")
    return result
