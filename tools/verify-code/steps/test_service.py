"""Runs the language service's tests under coverage.py; zero tests or no coverage report fails."""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from xml.etree import ElementTree

from steps.build_service import LOCKED, PROJECT, SERVICE, UV
from steps.common import (
    OUTPUT_DIRECTORY,
    CommandOutcome,
    Context,
    StepResult,
    failed,
    passed,
    reset_directory,
    tail,
)

NAME = "test_service"
RESULTS = "service"
JUNIT = "pytest.xml"
DATA = ".coverage"
REPORT = "coverage.xml"
COVERAGE = (*UV, "run", *LOCKED, "coverage")


@dataclass(frozen=True)
class SuiteRun:
    """What a JUnit report says: how many tests actually ran, how many failed or errored."""

    executed: int
    failed: int


def read_junit(path: Path) -> SuiteRun:
    """The counters summed over every suite of a JUnit report; a skipped test did not run."""
    suites = tuple(e for e in ElementTree.parse(path).getroot().iter() if e.tag == "testsuite")
    executed = sum(int(s.get("tests", "0")) - int(s.get("skipped", "0")) for s in suites)
    broken = sum(int(s.get("failures", "0")) + int(s.get("errors", "0")) for s in suites)
    return SuiteRun(executed, broken)


def judge(run: SuiteRun, pytest: CommandOutcome) -> StepResult:
    """At least one test ran, none failed, and pytest exited clean."""
    if run.executed == 0:
        return failed(NAME, f"nenhum teste de {SERVICE}/ executado: o passo não passa vazio")
    if run.failed > 0:
        return failed(NAME, f"{run.failed} teste(s) do serviço falharam", *tail(pytest.text))
    if not pytest.ok:
        return failed(NAME, "`pytest` terminou com erro sem teste reprovado", *tail(pytest.text))
    return passed(NAME, f"{run.executed} testes de {SERVICE}/ executados")


def pytest_command(results: Path) -> list[str]:
    """pytest under coverage.py, the data and the JUnit report both written into ``results``."""
    data, junit = str(results / DATA), str(results / JUNIT)
    return [*COVERAGE, "run", "--data-file", data, "-m", "pytest", "--junitxml", junit]


def coverage_command(results: Path) -> list[str]:
    """The XML report of the coverage data recorded into ``results``."""
    data, xml = str(results / DATA), str(results / REPORT)
    return [*COVERAGE, "xml", "--data-file", data, "-o", xml]


def run(ctx: Context) -> StepResult:
    if not (ctx.root / PROJECT).is_file():
        return failed(NAME, f"{PROJECT} não existe: não há suíte para rodar")
    results = ctx.out_dir / RESULTS
    reset_directory(results)
    pytest = ctx.run(pytest_command(results), ctx.root)
    if not (results / JUNIT).is_file():
        return failed(
            NAME,
            "nenhum relatório de teste foi escrito: o passo não passa vazio",
            *tail(pytest.text),
        )
    verdict = judge(read_junit(results / JUNIT), pytest)
    if not verdict.ok:
        return verdict
    xml = ctx.run(coverage_command(results), ctx.root)
    if not (results / REPORT).is_file():
        return failed(NAME, "nenhum relatório de cobertura XML foi escrito", *tail(xml.text))
    return passed(NAME, *verdict.details, f"cobertura em {OUTPUT_DIRECTORY}/{RESULTS}/{REPORT}")
