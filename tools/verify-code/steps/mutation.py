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
from steps.mutation_parts import SEPARATOR, Part, module_name, parts_of, split_module
from steps.mutation_policy import critical, read_policy

NAME = "mutation"


def listed(table: AreaTable) -> tuple[tuple[Area, ...], tuple[str, ...]]:
    """The critical modules of the area files, and what keeps the folder from giving them."""
    if table.problems:
        return (), table.problems
    modules = critical(table.areas)
    return modules, () if modules else (f"{FOLDER}: nenhuma área com Crítica `sim`",)


def jobs(root: Path, area: Area, areas: tuple[Area, ...]) -> tuple[str, ...]:
    """The names of the jobs of one module: its prefix, or one name per part when it has several."""
    count = mutation_core.part_count(root, area, areas)
    parts = parts_of(count)
    return tuple(module_name(area.prefix, part) for part in parts)


def names(root: Path) -> tuple[tuple[str, ...], tuple[str, ...]]:
    """The names CI hands to ``--module``, one job each, and the problems with the list."""
    table = read_areas(root)
    modules, problems = listed(table)
    return tuple(name for area in modules for name in jobs(root, area, table.areas)), problems


def wrong_part(root: Path, area: Area, areas: tuple[Area, ...], part: Part | None) -> str | None:
    """Why the part asked for is not one of the parts the area runs in, or None when it is."""
    if part is None:
        return None
    count = mutation_core.part_count(root, area, areas)
    if part.holds(count):
        return None
    return f"`{area.prefix}` roda em {count} parte(s); `{SEPARATOR}{part.label}` não é uma delas"


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
    if wrong := wrong_part(ctx.root, modules[0], table.areas, part):
        return failed(NAME, wrong)
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
    lines = (scope, *failures, *notes)
    summarize("\n".join(("### Mutação", "", *(f"- {line}" for line in lines), "")))
    return (failed if failures else passed)(NAME, *lines)
