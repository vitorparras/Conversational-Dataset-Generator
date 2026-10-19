"""Runs the property suites of both languages; a side that executed no property fails the step."""

from __future__ import annotations

from pathlib import Path

from steps.build_service import LOCKED, UV
from steps.common import CommandOutcome, Context, StepResult, failed, passed, reset_directory, tail
from steps.property_evidence import NONE, PropertyEvidence, from_junit, from_trx

NAME = "properties"
RESULTS = "properties"
CORE_SIDE = "núcleo (.NET)"
SERVICE_SIDE = "serviço (Python)"
CORE_PROJECT = "core/tests/Core.Tests.Properties/Core.Tests.Properties.csproj"
CORE_FILTER = ("--filter-trait", "Kind=Property")
SERVICE_FILTER = ("-m", "property")
JUNIT = "pytest.xml"


def core_evidence(results: Path) -> PropertyEvidence:
    reports = tuple(sorted(results.glob("*.trx")))
    return from_trx(reports) if reports else NONE


def service_evidence(results: Path) -> PropertyEvidence:
    report = results / JUNIT
    return from_junit(report) if report.is_file() else NONE


def problems(side: str, evidence: PropertyEvidence, outcome: CommandOutcome) -> list[str]:
    """What is wrong with one side: no property executed, a failure, or a bad exit."""
    if evidence.executed == 0:
        return [
            f"{side}: nenhuma propriedade executada; o passo não passa vazio",
            *tail(outcome.text),
        ]
    if evidence.failed > 0:
        return [f"{side}: {evidence.failed} propriedade(s) falharam", *tail(outcome.text)]
    if not outcome.ok:
        return [
            f"{side}: o comando terminou com erro sem propriedade reprovada",
            *tail(outcome.text),
        ]
    return []


def run(ctx: Context) -> StepResult:
    if not (ctx.root / CORE_PROJECT).is_file():
        return failed(NAME, f"{CORE_SIDE}: {CORE_PROJECT} não existe: não há propriedades")
    results = ctx.out_dir / RESULTS
    reset_directory(results)
    dotnet = ["dotnet", "test", "--project", CORE_PROJECT, "--report-xunit-trx", *CORE_FILTER]
    core = ctx.run([*dotnet, "--results-directory", str(results)], ctx.root)
    pytest = [*UV, "run", *LOCKED, "pytest", *SERVICE_FILTER, "--junitxml", str(results / JUNIT)]
    service = ctx.run(pytest, ctx.root)
    core_seen, service_seen = core_evidence(results), service_evidence(results)
    found = problems(CORE_SIDE, core_seen, core) + problems(SERVICE_SIDE, service_seen, service)
    if found:
        return failed(NAME, *found)
    return passed(
        NAME,
        f"{core_seen.executed} propriedades em {CORE_SIDE}",
        f"{service_seen.executed} propriedades em {SERVICE_SIDE}",
    )
