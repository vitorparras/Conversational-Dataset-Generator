"""Runs Stryker.NET over each critical core module, with a configuration generated from the list."""

from __future__ import annotations

import json
import re
from dataclasses import dataclass, replace
from pathlib import Path

from steps.areas_doc import Area, within
from steps.common import Context, reset_directory, tail
from steps.mutant import Mutant
from steps.mutation_judge import LanguageRun, ModuleResult, ceiling_problem, over_ceiling, timed
from steps.mutation_policy import MutationPolicy
from steps.stryker_report import read_stryker

SIDE = "núcleo (.NET)"
ROOT = "Core"
CORE = "core"
SOURCES = "core/src"
TESTS = "core/tests"
RESULTS = "mutation/core"
CONFIG = "stryker-config.json"
REPORT = "reports/mutation-report.json"
RESTORE = ("dotnet", "tool", "restore")
STRYKER = ("dotnet", "stryker", "--skip-version-check")
CONCURRENCY = 1


@dataclass(frozen=True)
class CoreTarget:
    """A critical area as Stryker sees it: the project that holds it and the files to mutate."""

    area: Area
    project: str
    pattern: str


def target(root: Path, area: Area) -> CoreTarget | None:
    """The longest-named project owning the area, and its folder; None when there is no code."""
    folder = root / SOURCES
    found = folder.iterdir() if folder.is_dir() else iter(())
    projects = [path.name for path in found if any(path.glob("*.csproj"))]
    owner = max((name for name in projects if within(area.prefix, name)), key=len, default=None)
    if owner is None:
        return None
    rest = area.prefix.removeprefix(owner).lstrip(".").replace(".", "/")
    if rest and not (folder / owner / rest).is_dir():
        return None
    return CoreTarget(area, owner, f"{rest}/**/*.cs" if rest else "**/*.cs")


def test_projects(root: Path, project: str) -> tuple[Path, ...]:
    """The test projects that reference ``project`` directly."""
    reference = re.compile(rf'ProjectReference Include="[^"]*[\\/]{re.escape(project)}\.csproj"')
    candidates = sorted((root / TESTS).glob("*/*.csproj"))
    return tuple(p for p in candidates if reference.search(p.read_text(encoding="utf-8")))


def configuration(core: CoreTarget, tests: tuple[Path, ...]) -> str:
    """The Stryker configuration of one module; Stryker never reproves, the step judges."""
    options = {
        "project": f"{core.project}.csproj",
        "test-projects": [str(path) for path in tests],
        "mutate": [core.pattern],
        "test-runner": "mtp",
        "concurrency": CONCURRENCY,
        "reporters": ["json"],
        "thresholds": {"high": 100, "low": 0, "break": 0},
    }
    return json.dumps({"stryker-config": options}, indent=2) + "\n"


def mutate(ctx: Context, core: CoreTarget) -> ModuleResult:
    """Writes the module's configuration, runs Stryker and reads its report."""
    tests = test_projects(ctx.root, core.project)
    if not tests:
        return ModuleResult(
            core.area, (), (f"nenhum projeto de {TESTS}/ referencia {core.project}",)
        )
    output = ctx.out_dir / RESULTS / core.area.prefix
    reset_directory(output)
    config = output / CONFIG
    config.write_text(configuration(core, tests), encoding="utf-8")
    command = [*STRYKER, "--config-file", str(config), "--output", str(output)]
    # Beside core/Core.slnx Stryker takes the whole solution and drops the configured test projects.
    outcome = ctx.run(command, tests[0].parent)
    report = output / REPORT
    if not report.is_file():
        missing = ("o Stryker não escreveu relatório", *tail(outcome.text))
        return ModuleResult(core.area, (), missing)
    mutants: tuple[Mutant, ...] = read_stryker(report, ctx.root.resolve())
    problems = () if outcome.ok else ("o Stryker terminou com erro", *tail(outcome.text))
    return ModuleResult(core.area, mutants, problems)


def run_core(ctx: Context, areas: tuple[Area, ...], policy: MutationPolicy) -> LanguageRun:
    """Every core module in turn; once the time spent passes the ceiling, the rest does not run."""
    targets = tuple(core for area in areas if (core := target(ctx.root, area)) is not None)
    absent = tuple(area for area in areas if area not in {core.area for core in targets})
    if not targets:
        return LanguageRun(SIDE, absent=absent)
    restore = ctx.run(list(RESTORE), ctx.root / CORE)
    if not restore.ok:
        missing = "`dotnet tool restore` falhou em core/: o Stryker.NET de core/dotnet-tools.json"
        return LanguageRun(SIDE, problems=(f"{missing} não está disponível", *tail(restore.text)))
    results: list[ModuleResult] = []
    spent = 0.0
    for core in targets:
        measured = timed(lambda core=core: mutate(ctx, core))
        results.append(replace(measured.value, seconds=measured.seconds))
        spent += measured.seconds
        if over_ceiling(policy, spent):
            break
    return LanguageRun(
        SIDE, tuple(results), absent, ceiling(policy, targets, results), seconds=spent
    )


def ceiling(
    policy: MutationPolicy, targets: tuple[CoreTarget, ...], results: list[ModuleResult]
) -> tuple[str, ...]:
    """The ceiling finding naming the module that passed it, and the modules left unrun."""
    spent = sum(result.seconds for result in results)
    if not over_ceiling(policy, spent):
        return ()
    left = tuple(f"`{core.area.prefix}`" for core in targets[len(results) :])
    passed = ceiling_problem(policy, (results[-1].area.prefix,), spent)
    return (passed, f"não rodaram por causa do teto: {', '.join(left)}") if left else (passed,)
