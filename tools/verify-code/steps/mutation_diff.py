"""The mutation jobs of a pull request (decisão 0059): of the modules the diff reaches, those with a
file of their own in the diff, in the parts those files weigh; the reasons say, per module, which
of its files run now and which wait for the nightly run of main."""

from __future__ import annotations

from pathlib import Path

from steps import mutation, reach
from steps.areas_doc import Area, read_areas
from steps.common import Context
from steps.mutation_parts import split_module
from steps.mutation_scope import DiffScope, read

NIGHT = "mutação noturna do main"


def said(root: Path, area: Area, areas: tuple[Area, ...], scope: DiffScope) -> str:
    """The line that tells which files of the module the pull request mutates."""
    every = mutation.files(root, area, areas)
    kept = scope.keeps(every)
    if not kept:
        return f"- `{area.prefix}`: nenhum arquivo do módulo no diff; fica para a {NIGHT}"
    rest = "o módulo inteiro está no diff" if kept == every else f"os outros ficam para a {NIGHT}"
    listed = ", ".join(kept)
    return f"- `{area.prefix}`: muta {len(kept)} de {len(every)} arquivos ({listed}); {rest}"


def select(ctx: Context) -> tuple[tuple[str, ...], list[str]]:
    """The jobs of the modules the diff reaches with files of their own and the lines that say
    why; every reached job, whole, when the diff cannot be read."""
    names, reasons = reach.select(ctx)
    scope = read(ctx)
    if scope.paths is None or not names:
        return names, reasons
    table = read_areas(ctx.root)
    known = {area.prefix: area for area in table.areas}
    reached = [known[p] for p in dict.fromkeys(split_module(n)[0] for n in names) if p in known]
    chosen = (mutation.jobs(ctx.root, area, table.areas, scope) for area in reached)
    lines = (said(ctx.root, area, table.areas, scope) for area in reached)
    return tuple(job for jobs in chosen for job in jobs), [*reasons, *lines]
