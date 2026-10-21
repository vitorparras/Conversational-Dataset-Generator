"""Runs the core test suites with Cobertura coverage; a test project that executes nothing fails.

Skipped tests do not fail the step, but each project that skipped some is named with the reasons,
whether the step passes or fails."""

from __future__ import annotations

import re
from dataclasses import dataclass, replace
from pathlib import Path
from xml.etree import ElementTree

from steps.build_core import SOLUTION
from steps.common import CommandOutcome, Context, StepResult, failed, passed, reset_directory, tail

NAME = "test_core"
RESULTS = "core"


@dataclass(frozen=True)
class TestRun:
    """What one TRX report says: the test assembly, how many tests ran, failed or were skipped."""

    project: str
    executed: int
    failed: int
    skipped: int
    skip_reasons: tuple[str, ...]


def test_projects(solution: Path) -> tuple[str, ...]:
    """The test projects the solution lists, by project name."""
    text = solution.read_text(encoding="utf-8")
    paths = re.findall(r'Path="([^"]+\.csproj)"', text)
    normalized = (p.replace("\\", "/") for p in paths)
    return tuple(sorted(Path(p).stem for p in normalized if p.startswith("tests/")))


def read_trx(path: Path) -> TestRun:
    """The counters and the test assembly of a TRX file; namespaced tags are matched by suffix."""
    root = ElementTree.parse(path).getroot()
    counters = next(e for e in root.iter() if e.tag.endswith("}Counters"))
    method = next((e for e in root.iter() if e.tag.endswith("}TestMethod")), None)
    code_base = method.get("codeBase", "") if method is not None else ""
    project = Path(code_base.replace("\\", "/")).stem
    reasons = {
        (e.findtext("*/{*}StdOut") or "").strip()
        for e in root.iter()
        if e.tag.endswith("}UnitTestResult") and e.get("outcome") == "NotExecuted"
    }
    return TestRun(
        project,
        int(counters.get("executed", "0")),
        int(counters.get("failed", "0")),
        int(counters.get("notExecuted", "0")),
        tuple(sorted(reason for reason in reasons if reason)),
    )


def describe_skips(run: TestRun) -> str:
    """One line per project that skipped tests, with the reasons the tests gave."""
    reasons = "; ".join(run.skip_reasons) or "sem motivo declarado"
    return f"{run.project}: {run.skipped} teste(s) saltado(s) ({reasons})"


def judge(
    runs: tuple[TestRun, ...], projects: tuple[str, ...], dotnet: CommandOutcome
) -> StepResult:
    """The verdict, followed by every project that skipped tests, whether the step passed or not."""
    result = verdict(runs, projects, dotnet)
    skips = tuple(describe_skips(run) for run in runs if run.skipped > 0)
    return replace(result, details=(*result.details, *skips))


def verdict(
    runs: tuple[TestRun, ...], projects: tuple[str, ...], dotnet: CommandOutcome
) -> StepResult:
    """Every test project ran at least one test, none failed, and `dotnet test` exited clean."""
    executed = {run.project: run.executed for run in runs if run.executed > 0}
    silent = [p for p in projects if p not in executed]
    if silent:
        return failed(
            NAME,
            *(f"o projeto de teste {p} não executou nenhum teste" for p in silent),
            *tail(dotnet.text),
        )
    failures = sum(run.failed for run in runs)
    if failures > 0:
        return failed(NAME, f"{failures} teste(s) do núcleo falharam", *tail(dotnet.text))
    if not dotnet.ok:
        return failed(
            NAME, "`dotnet test` terminou com erro sem teste reprovado", *tail(dotnet.text)
        )
    return passed(NAME, f"{sum(executed.values())} testes executados em {len(executed)} projetos")


def run(ctx: Context) -> StepResult:
    solution = ctx.root / SOLUTION
    if not solution.is_file():
        return failed(NAME, f"{SOLUTION} não existe: não há suíte para rodar")
    results = ctx.out_dir / RESULTS
    reset_directory(results)
    command = [
        "dotnet",
        "test",
        SOLUTION,
        "--no-build",
        "--coverage",
        "--coverage-output-format",
        "cobertura",
        "--report-xunit-trx",
        "--results-directory",
        str(results),
    ]
    outcome = ctx.run(command, ctx.root)
    reports = tuple(read_trx(trx) for trx in sorted(results.glob("*.trx")))
    if not reports:
        return failed(
            NAME,
            "nenhum relatório de teste foi escrito: o passo não passa vazio",
            *tail(outcome.text),
        )
    result = judge(reports, test_projects(solution), outcome)
    if result.ok and not any(results.glob("*.cobertura.xml")):
        return failed(NAME, "nenhum relatório de cobertura Cobertura foi escrito")
    return result
