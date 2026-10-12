"""Run the core test suites with coverage; a project that executes nothing is a failure."""

from __future__ import annotations

import re
from pathlib import Path

from steps.common import Context, StepResult, clean_directory, report_skips, run_command
from steps.reports import FAILED, SKIPPED, TestOutcome, read_trx

NAME = "test_core"
GROUP = "core"
DESCRIPTION = "roda as suítes do núcleo (.NET) com cobertura Cobertura"

RESULTS = "core"


def expected_test_projects(solution: Path) -> list[str]:
    """The test projects the solution lists, by assembly name."""
    text = solution.read_text(encoding="utf-8")
    return sorted(
        {
            Path(match).stem
            for match in re.findall(r'Path="([^"]+\.csproj)"', text)
            if "/tests/" in "/" + match.replace("\\", "/")
        }
    )


def evaluate(ctx: Context, outcomes: list[TestOutcome], projects: list[str], result: StepResult) -> None:
    """Judge the outcomes: nothing executed, a project without tests, a failure or a skip."""
    if not outcomes:
        result.fail("nenhum teste executado no núcleo: o passo não passa vazio")
        return
    for project in projects:
        own = [o for o in outcomes if o.source == f"{project}.dll"]
        executed = [o for o in own if o.outcome != SKIPPED]
        if not executed:
            result.fail(f"o projeto de teste {project} não executou nenhum teste")
        else:
            result.note(f"{project}: {len(executed)} executados, {len(own) - len(executed)} saltados")
    for outcome in outcomes:
        if outcome.outcome == FAILED:
            result.fail(f"teste falhou: {outcome.source} {outcome.name}")
    report_skips(
        result, ctx, [f"{o.source} {o.name}" for o in outcomes if o.outcome == SKIPPED], "núcleo"
    )


def run(ctx: Context) -> StepResult:
    result = StepResult()
    solution = ctx.root / "core" / "Core.slnx"
    if not solution.is_file():
        result.fail("core/Core.slnx não existe: não há suíte para rodar")
        return result
    results = ctx.out_dir / RESULTS
    clean_directory(results)
    command = [
        "dotnet", "test", str(solution), "--no-build",
        "--collect:XPlat Code Coverage", "--logger", "trx;LogFilePrefix=core",
        "--results-directory", str(results),
    ]
    run_command(command, ctx.root, result)
    trx_files = sorted(results.rglob("*.trx"))
    if not trx_files:
        result.fail("o dotnet test não escreveu nenhum relatório TRX")
        return result
    outcomes = [outcome for trx in trx_files for outcome in read_trx(trx)]
    evaluate(ctx, outcomes, expected_test_projects(solution), result)
    if not sorted(results.rglob("coverage.cobertura.xml")):
        result.fail("nenhum relatório de cobertura Cobertura foi escrito")
    return result
