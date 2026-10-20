"""Runs mutation testing over exactly the critical modules of docs/operacao/014, in both languages.

The list of modules, the survivor ceiling and the time ceiling are read from the document on every
run; the configurations of Stryker.NET and of mutmut are generated from that list into the output
directory, so no scope lives in a tool's configuration file.
"""

from __future__ import annotations

from steps import mutation_core, mutation_service
from steps.areas_doc import DOCUMENT, read_areas, within
from steps.common import Context, StepResult, failed, passed
from steps.coverage_by_area import summarize
from steps.floors import Verdict
from steps.mutation_judge import judge_language
from steps.mutation_policy import critical, read_policy

NAME = "mutation"


def run(ctx: Context) -> StepResult:
    policy = read_policy(ctx.root)
    if isinstance(policy, tuple):
        return failed(NAME, *policy)
    table = read_areas(ctx.root)
    if table.problems:
        return failed(NAME, *table.problems)
    modules = critical(table.areas, policy)
    if not modules:
        return failed(NAME, f"{DOCUMENT}: nenhuma área de piso {policy.critical_floor} %")
    core = tuple(area for area in modules if within(area.prefix, mutation_core.ROOT))
    service = tuple(area for area in modules if within(area.prefix, mutation_service.ROOT))
    stray = (area for area in modules if area not in (*core, *service))
    verdicts = (
        Verdict(tuple(f"`{a.prefix}`: nenhuma ferramenta de mutação o alcança" for a in stray)),
        judge_language(mutation_core.run_core(ctx, core, policy), policy),
        judge_language(mutation_service.run_service(ctx, service, policy), policy),
    )
    failures = tuple(line for verdict in verdicts for line in verdict.failures)
    notes = tuple(line for verdict in verdicts for line in verdict.notes)
    rule = f"{policy.survivor_ceiling} % de sobreviventes por módulo, {policy.minutes} min"
    scope = f"{len(modules)} módulos críticos (piso {policy.critical_floor} %); limite {rule}"
    lines = (scope, *failures, *notes)
    summarize("\n".join(("### Mutação", "", *(f"- {line}" for line in lines), "")))
    return (failed if failures else passed)(NAME, *lines)
