"""Runs mutation testing over exactly the critical modules of docs/operacao/areas, in both
languages.

The list of modules, the survivor ceiling and the time ceiling are read from the document on every
run; the configurations of Stryker.NET and of mutmut are generated from that list into the output
directory, so no scope lives in a tool's configuration file. With a module in the context only that
one runs, which is how CI gives each module a job and a ceiling of its own; without one, every
module runs in series, each with its own ceiling.
"""

from __future__ import annotations

from pathlib import Path

from steps import mutation_core, mutation_service
from steps.areas_doc import FOLDER, Area, AreaTable, read_areas, within
from steps.common import Context, StepResult, failed, passed
from steps.coverage_by_area import summarize
from steps.floors import Verdict
from steps.mutation_judge import judge_language
from steps.mutation_parts import SEPARATOR, Part, dealt, inert, module_name, parts_of, split_module
from steps.mutation_policy import critical, read_policy
from steps.mutation_scope import DiffScope, read

NAME = "mutation"
IDLE = "nenhuma fatia com código a mutar nos arquivos do diff; a suíte não roda"


def listed(table: AreaTable) -> tuple[tuple[Area, ...], tuple[str, ...]]:
    """The critical modules of the area files, and what keeps the folder from giving them."""
    if table.problems:
        return (), table.problems
    modules = critical(table.areas)
    return modules, () if modules else (f"{FOLDER}: nenhuma área com Crítica `sim`",)


def jobs(
    root: Path, area: Area, areas: tuple[Area, ...], scope: DiffScope | None = None
) -> tuple[str, ...]:
    """The names of the jobs of one module: its prefix, or one name per part when it has several;
    none in a run of the diff that changed no file of it, nor for a part with no code to mutate."""
    if scope is not None and not scope.keeps(files(root, area, areas)):
        return ()
    count = mutation_core.part_count(root, area, areas, scope)
    left: frozenset[Part | None] = frozenset() if scope is None else idle(root, area, areas, scope)
    return tuple(module_name(area.prefix, part) for part in parts_of(count) if part not in left)


def idle(
    root: Path, area: Area, areas: tuple[Area, ...], scope: DiffScope
) -> frozenset[Part | None]:
    """In a run of the diff, the parts of a core module whose slices hold no code Stryker could
    mutate, so they run no suite (#935); None when it is one part. None in the nightly run."""
    core_module = within(area.prefix, mutation_core.ROOT) and scope.paths is not None
    core = mutation_core.scoped(root, area, areas, scope) if core_module else None
    if core is None:
        return frozenset()
    found = mutation_core.pieces(root, core)
    count = mutation_core.part_count(root, area, areas, scope)
    held = ((part, found if part is None else dealt(found, part)) for part in parts_of(count))
    return frozenset(part for part, mine in held if inert(mine))


def files(root: Path, area: Area, areas: tuple[Area, ...]) -> tuple[str, ...]:
    """The files of the whole module, from the root of the repository, in either language."""
    if within(area.prefix, mutation_core.ROOT):
        return mutation_core.files(root, area, areas)
    return mutation_service.files(root, area, areas)


def names(root: Path) -> tuple[tuple[str, ...], tuple[str, ...]]:
    """The names CI hands to ``--module``, one job each, and the problems with the list."""
    table = read_areas(root)
    modules, problems = listed(table)
    return tuple(name for area in modules for name in jobs(root, area, table.areas)), problems


def wrong_part(ctx: Context, area: Area, areas: tuple[Area, ...], part: Part | None) -> str | None:
    """Why the part asked for is not one of the parts the area runs in, or None when it is."""
    if part is None:
        return None
    count = mutation_core.part_count(ctx.root, area, areas, read(ctx))
    if part.holds(count):
        return None
    return f"`{area.prefix}` roda em {count} parte(s); `{SEPARATOR}{part.label}` não é uma delas"


def halted(
    ctx: Context, area: Area, areas: tuple[Area, ...], part: Part | None
) -> StepResult | None:
    """How the run of one module ends before Stryker: refused for a part the area does not run in,
    passed for a part of the diff with no code to mutate; None when Stryker runs."""
    if wrong := wrong_part(ctx, area, areas, part):
        return failed(NAME, wrong)
    if ctx.module and part in idle(ctx.root, area, areas, read(ctx)):
        return passed(NAME, f"`{ctx.module}`: {IDLE}")
    return None


def run(ctx: Context) -> StepResult:
    policy = read_policy(ctx.root)
    if isinstance(policy, tuple):
        return failed(NAME, *policy)
    table = read_areas(ctx.root)
    every, problems = listed(table)
    if problems:
        return failed(NAME, *problems)
    asked, part, malformed = split_module(ctx.module)
    if malformed:
        return failed(NAME, malformed)
    modules = tuple(area for area in every if asked in (None, area.prefix))
    if not modules:
        known = ", ".join(f"`{area.prefix}`" for area in every)
        return failed(NAME, f"`{ctx.module}` não é módulo crítico de {FOLDER}; são: {known}")
    if (stop := halted(ctx, modules[0], table.areas, part)) is not None:
        return stop
    core = tuple(area for area in modules if within(area.prefix, mutation_core.ROOT))
    service = tuple(area for area in modules if within(area.prefix, mutation_service.ROOT))
    stray = (
        f"`{a.prefix}`: nenhuma ferramenta de mutação o alcança"
        for a in modules
        if a not in (*core, *service)
    )
    verdicts = (
        Verdict(tuple(stray)),
        judge_language(mutation_core.run_core(ctx, core, table.areas, policy), policy),
        judge_language(mutation_service.run_service(ctx, service, table.areas, policy), policy),
    )
    failures = tuple(line for verdict in verdicts for line in verdict.failures)
    notes = tuple(line for verdict in verdicts for line in verdict.notes)
    rule = f"{policy.survivor_ceiling} % de sobreviventes e {policy.minutes} min por módulo"
    which = f"módulo `{ctx.module}`" if ctx.module else f"{len(modules)} módulos críticos"
    scope = f"{which} (coluna Crítica, {len(every)} no total); limite {rule}"
    lines = (scope, *((read(ctx).line,) if ctx.base else ()), *failures, *notes)
    summarize("\n".join(("### Mutação", "", *(f"- {line}" for line in lines), "")))
    return (failed if failures else passed)(NAME, *lines)
