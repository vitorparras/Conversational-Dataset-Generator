"""Runs Stryker.NET over each critical core module, with a configuration generated from the list."""

from __future__ import annotations

import json
import re
from dataclasses import dataclass
from pathlib import Path

from steps.areas_doc import Area, within
from steps.common import Context, reset_directory, tail
from steps.mutant import Mutant
from steps.mutation_judge import LanguageRun, ModuleResult, in_turn
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
    patterns: tuple[str, ...]


def target(root: Path, area: Area, areas: tuple[Area, ...]) -> CoreTarget | None:
    """The longest-named project owning the area and its folder, minus the folders of the areas
    nested in it, which own their own code; None when there is no code."""
    folder = root / SOURCES
    found = folder.iterdir() if folder.is_dir() else iter(())
    projects = [path.name for path in found if any(path.glob("*.csproj"))]
    owner = max((name for name in projects if within(area.prefix, name)), key=len, default=None)
    if owner is None:
        return None
    rest = area.prefix.removeprefix(owner).lstrip(".").replace(".", "/")
    if rest and not (folder / owner / rest).is_dir():
        return None
    inner = (a.prefix for a in areas if a.prefix != area.prefix and within(a.prefix, area.prefix))
    nested = (prefix.removeprefix(owner).lstrip(".").replace(".", "/") for prefix in inner)
    globs = (f"{rest}/**/*.cs" if rest else "**/*.cs", *(f"!{path}/**/*.cs" for path in nested))
    return CoreTarget(area, owner, tuple(globs))


def test_projects(root: Path, project: str) -> tuple[Path, ...]:
    """The test projects that reference ``project`` directly."""
    reference = re.compile(rf'ProjectReference Include="[^"]*[\\/]{re.escape(project)}\.csproj"')
    candidates = sorted((root / TESTS).glob("*/*.csproj"))
    return tuple(p for p in candidates if reference.search(p.read_text(encoding="utf-8")))


def configuration(core: CoreTarget, tests: tuple[Path, ...]) -> str:
    """The Stryker configuration of one module; Stryker never reproves, the step judges. It sets
    no method filter: that one hides the whole call chain, so what the 017 leaves out is filtered
    from the report (`read_stryker`)."""
    options = {
        "project": f"{core.project}.csproj",
        "test-projects": [str(path) for path in tests],
        "mutate": list(core.patterns),
        "test-runner": "mtp",
        "concurrency": CONCURRENCY,
        "reporters": ["json"],
        "thresholds": {"high": 100, "low": 0, "break": 0},
    }
    return json.dumps({"stryker-config": options}, indent=2) + "\n"


def mutate(ctx: Context, core: CoreTarget, seconds: float, policy: MutationPolicy) -> ModuleResult:
    """Writes the module's configuration, runs Stryker for at most ``seconds``, reads its report."""
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
    outcome = ctx.run_for(command, tests[0].parent, seconds)
    report = output / REPORT
    if not report.is_file():
        missing = ("o Stryker não escreveu relatório", *tail(outcome.text))
        return ModuleResult(core.area, (), missing)
    mutants: tuple[Mutant, ...] = read_stryker(
        report, ctx.root.resolve(), policy.skipped_argument_of
    )
    problems = () if outcome.ok else ("o Stryker terminou com erro", *tail(outcome.text))
    return ModuleResult(core.area, mutants, problems)


def run_core(
    ctx: Context, areas: tuple[Area, ...], everything: tuple[Area, ...], policy: MutationPolicy
) -> LanguageRun:
    """Every core module in turn, each cut at what is left of the ceiling; then the rest stops."""
    targets = tuple(c for area in areas if (c := target(ctx.root, area, everything)) is not None)
    absent = tuple(area for area in areas if area not in {core.area for core in targets})
    if not targets:
        return LanguageRun(SIDE, absent=absent)
    restore = ctx.run(list(RESTORE), ctx.root / CORE)
    if not restore.ok:
        missing = "`dotnet tool restore` falhou em core/: o Stryker.NET de core/dotnet-tools.json"
        return LanguageRun(SIDE, problems=(f"{missing} não está disponível", *tail(restore.text)))
    results, findings, spent = in_turn(
        policy,
        targets,
        lambda core: core.area,
        lambda core, seconds: mutate(ctx, core, seconds, policy),
    )
    return LanguageRun(SIDE, results, absent, findings, seconds=spent)
