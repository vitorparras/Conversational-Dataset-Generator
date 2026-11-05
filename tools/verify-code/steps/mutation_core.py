"""Runs Stryker.NET over each critical core module, with a configuration generated from the list."""

from __future__ import annotations

import json
import re
import secrets
import shutil
from collections.abc import Generator
from dataclasses import dataclass, replace
from pathlib import Path

from steps.areas_doc import Area, within
from steps.common import Context, reset_directory, tail
from steps.mutant import Mutant
from steps.mutation_database import DOCKER, discard_database, reusing, starts_database
from steps.mutation_initial import explain
from steps.mutation_judge import LanguageRun, ModuleResult, each
from steps.mutation_members import slices
from steps.mutation_parts import Part, Slice, module_name, parts_of, share, split_module
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
STRYKER = ("dotnet", "stryker", "--skip-version-check", "--log-to-file")
CONCURRENCY = 1
TOKEN_BYTES = 8


@dataclass(frozen=True)
class CoreTarget:
    """A critical area as Stryker sees it: the project that holds it and the files to mutate, all of
    them or the share of one part."""

    area: Area
    project: str
    patterns: tuple[str, ...]
    part: Part | None = None

    @property
    def shown(self) -> Area:
        """The area as the report names it: with its part, when it runs in parts."""
        if self.part is None:
            return self.area
        return replace(self.area, name=f"{self.area.name}, parte {self.part.label}")


def whole(root: Path, area: Area, areas: tuple[Area, ...]) -> CoreTarget | None:
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


def sources(root: Path, core: CoreTarget) -> list[str]:
    """The files of the whole area, as paths from its project, minus the ones of nested areas."""
    project = root / SOURCES / core.project
    lines = [(pattern.startswith("!"), pattern.removeprefix("!")) for pattern in core.patterns]
    wanted = {path for out, pattern in lines if not out for path in project.glob(pattern)}
    left = {path for out, pattern in lines if out for path in project.glob(pattern)}
    built = {"bin", "obj"}
    kept = (path.relative_to(project) for path in wanted - left)
    return [found.as_posix() for found in kept if built.isdisjoint(found.parts)]


def pieces(root: Path, core: CoreTarget) -> list[Slice]:
    """Every file of the area cut between the members of its types: what the parts deal out. The
    spans count the characters of the text Stryker reads, byte order mark aside."""
    project = root / SOURCES / core.project
    texts = {
        name: (project / name).read_bytes().decode("utf-8-sig") for name in sources(root, core)
    }
    return [Slice(name, a, b, w) for name, text in texts.items() for a, b, w in slices(text)]


def part_count(root: Path, area: Area, areas: tuple[Area, ...]) -> int:
    """In how many parts the area is mutated: those its file asks for, but never more than it has
    slices; one for an area with no code, outside the core or without the field."""
    core = whole(root, area, areas)
    if core is None or area.parts == 1 or not within(area.prefix, ROOT):
        return 1
    return max(1, min(area.parts, len(pieces(root, core))))


def target(
    root: Path, area: Area, areas: tuple[Area, ...], part: Part | None = None
) -> CoreTarget | None:
    """The area as ``whole`` finds it, or only the files of ``part`` when it is given."""
    core = whole(root, area, areas)
    if core is None or part is None or part.of == 1:
        return core
    return replace(core, patterns=share(pieces(root, core), part), part=part)


def part_targets(
    root: Path, area: Area, areas: tuple[Area, ...], asked: Part | None
) -> list[CoreTarget]:
    """The targets of an area: one per part, or only the part asked for; one for the whole area."""
    count = part_count(root, area, areas)
    parts = parts_of(count)
    chosen = [part for part in parts if asked is None or part == asked or count == 1]
    found = (target(root, area, areas, part) for part in chosen)
    return [core for core in found if core is not None]


def holders(project: str, area: str | None) -> Generator[str]:
    """The prefixes that hold ``area`` inside ``project``, the area first and the project last."""
    prefix = area or project
    while within(prefix, project):
        yield prefix
        prefix = prefix.rpartition(".")[0]


def test_projects(root: Path, project: str, area: str | None = None) -> tuple[Path, ...]:
    """The test projects of ``project`` that reference it directly. An area that has a project of
    its own (``Core.Persistence.Migrations.Tests`` for ``Core.Persistence.Migrations``) gets that
    one alone; an area nested in another that has one (``Core.Persistence.Custody.Chain.Append`` in
    ``Core.Persistence.Custody.Chain``) gets the project of the nearest area that holds it;
    otherwise the project of what holds it: ``<Project>.Tests``, or the one of a project
    it is nested in (``Core.Boundary.Tests`` for ``Core.Boundary.Contracts``). The cross-cutting
    suites (``Core.Tests.Integration``) are not the module's own and stay out."""
    reference = re.compile(rf'ProjectReference Include="[^"]*[\\/]{re.escape(project)}\.csproj"')

    def owns(path: Path) -> bool:
        stem = path.stem.removesuffix(".Tests")
        return stem != path.stem and within(project, stem)

    def references(path: Path) -> bool:
        return reference.search(path.read_text(encoding="utf-8")) is not None

    candidates = sorted((root / TESTS).glob("*/*.csproj"))
    names = {f"{prefix}.Tests" for prefix in holders(project, area)}
    owned = [p for p in candidates if p.stem in names and references(p)]
    nearest = max(owned, key=lambda p: len(p.stem), default=None)
    own = () if nearest is None else (nearest,)
    return own or tuple(p for p in candidates if owns(p) and references(p))


def configuration(core: CoreTarget, tests: tuple[Path, ...]) -> str:
    """The Stryker configuration of one module; Stryker never reproves, the step judges. It sets
    no method filter: that one hides the whole call chain, so what the 017 leaves out is filtered
    from the report (`read_stryker`). One worker in every module keeps the verdicts the same from
    one run to the next."""
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
    tests = test_projects(ctx.root, core.project, core.area.prefix)
    if not tests:
        return ModuleResult(
            core.shown, (), (f"nenhum projeto de {TESTS}/ referencia {core.project}",)
        )
    output = ctx.out_dir / RESULTS / module_name(core.area.prefix, core.part).replace("/", "-")
    reset_directory(output)
    config = output / CONFIG
    config.write_text(configuration(core, tests), encoding="utf-8")
    command = [*STRYKER, "--config-file", str(config), "--output", str(output)]
    # Without the docker CLI the step could not remove what the run leaves, so nothing is reused.
    reuse = (
        secrets.token_hex(TOKEN_BYTES) if starts_database(tests) and shutil.which(DOCKER) else None
    )
    try:
        # Beside core/Core.slnx Stryker takes the whole solution and drops the configured tests.
        with reusing(reuse):
            outcome = ctx.run_for(command, tests[0].parent, seconds)
    finally:
        if reuse is not None:
            discard_database(ctx, reuse)
    report = output / REPORT
    if not report.is_file():
        explained = explain(output, outcome.text)
        missing = ("o Stryker não escreveu relatório", *explained, *tail(outcome.text))
        return ModuleResult(core.shown, (), missing)
    mutants: tuple[Mutant, ...] = read_stryker(
        report, ctx.root.resolve(), policy.skipped_argument_of
    )
    problems = () if outcome.ok else ("o Stryker terminou com erro", *tail(outcome.text))
    return ModuleResult(core.shown, mutants, problems)


def run_core(
    ctx: Context, areas: tuple[Area, ...], everything: tuple[Area, ...], policy: MutationPolicy
) -> LanguageRun:
    """Every core module in turn, each cut at the ceiling it has to itself."""
    _, asked, _ = split_module(ctx.module)
    found = (part_targets(ctx.root, area, everything, asked) for area in areas)
    targets = tuple(core for cores in found for core in cores)
    absent = tuple(area for area in areas if area not in {core.area for core in targets})
    if not targets:
        return LanguageRun(SIDE, absent=absent)
    restore = ctx.run(list(RESTORE), ctx.root / CORE)
    if not restore.ok:
        missing = "`dotnet tool restore` falhou em core/: o Stryker.NET de core/dotnet-tools.json"
        return LanguageRun(SIDE, problems=(f"{missing} não está disponível", *tail(restore.text)))
    results, findings = each(
        policy, targets, lambda core, seconds: mutate(ctx, core, seconds, policy)
    )
    return LanguageRun(SIDE, results, absent, findings)
