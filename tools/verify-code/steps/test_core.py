"""Runs the core test suites with Cobertura coverage; a test project that executes nothing fails."""

from __future__ import annotations

import re
import xml.etree.ElementTree as ElementTree
from dataclasses import dataclass
from pathlib import Path

from steps.build_core import SOLUTION
from steps.common import CommandOutcome, Context, StepResult, failed, passed, reset_directory, tail

NAME = "test_core"
RESULTS = "core"


@dataclass(frozen=True)
class TestRun:
    """What one TRX report says: which test assembly ran, how many tests, how many failed."""

    project: str
    executed: int
    failed: int


def test_projects(solution: Path) -> tuple[str, ...]:
    """The test projects the solution lists, by project name."""
    text = solution.read_text(encoding="utf-8")
    paths = re.findall(r'Path="([^"]+\.csproj)"', text)
    return tuple(sorted(Path(p).stem for p in paths if p.replace("\\", "/").startswith("tests/")))


def read_trx(path: Path) -> TestRun:
    """The counters and the test assembly of a TRX file; tags carry a namespace, so match by suffix."""
    root = ElementTree.parse(path).getroot()
    counters = next(e for e in root.iter() if e.tag.endswith("}Counters"))
    method = next((e for e in root.iter() if e.tag.endswith("}TestMethod")), None)
    code_base = method.get("codeBase", "") if method is not None else ""
    project = Path(code_base.replace("\\", "/")).stem
    return TestRun(project, int(counters.get("executed", "0")), int(counters.get("failed", "0")))


def judge(
    runs: tuple[TestRun, ...], projects: tuple[str, ...], dotnet: CommandOutcome
) -> StepResult:
    """Every test project ran at least one test, none failed, and `dotnet test` itself exited clean."""
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
