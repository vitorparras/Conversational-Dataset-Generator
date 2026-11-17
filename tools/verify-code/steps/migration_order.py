"""A migration a pull request adds is named by moment and sorts after every one the base has."""

from __future__ import annotations

import re

from steps.common import Context, StepResult, failed, passed, tail

NAME = "migration_order"
FOLDER = "core/src/Core.Persistence/Migrations/Sql"
MOMENT_NAME = re.compile(r"^\d{12}-[^/]+\.sql$")


def names_in(text: str) -> list[str]:
    """The file names of a list of paths, one per line."""
    return [line.rsplit("/", 1)[-1] for line in text.splitlines() if line.endswith(".sql")]


def problems_of(added: list[str], published: list[str]) -> list[str]:
    """What is wrong with the added names: not a moment, or not after the last published one."""
    last = max(published, default="")
    found = [
        f"{name}: a migração nova leva o nome AAAAMMDDHHMM-nome.sql"
        for name in added
        if not MOMENT_NAME.match(name)
    ]
    found += [
        f"{name}: ordena antes de {last}, que a base já tem; traga o main e renomeie para depois"
        for name in added
        if MOMENT_NAME.match(name) and name <= last
    ]
    return found


def run(ctx: Context) -> StepResult:
    if ctx.base is None:
        return failed(NAME, "migration_order exige --base <ref> (por exemplo, --base origin/main)")
    diff = ["git", "diff", "--name-only", "--diff-filter=A", f"{ctx.base}...HEAD", "--", FOLDER]
    listing = ["git", "ls-tree", "-r", "--name-only", ctx.base, "--", f"{FOLDER}/"]
    added, published = ctx.run(diff, ctx.root), ctx.run(listing, ctx.root)
    if not (added.ok and published.ok):
        return failed(
            NAME, "a leitura das migrações contra a base falhou", *tail(added.text + published.text)
        )
    found = problems_of(names_in(added.text), names_in(published.text))
    if found:
        return failed(NAME, *found)
    news, olds = len(names_in(added.text)), len(names_in(published.text))
    return passed(NAME, f"{news} migrações novas, todas depois das {olds} da base")
