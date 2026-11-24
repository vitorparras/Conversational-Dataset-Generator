"""What a mutation run covers (decisão 0059): with a base, only the files of each critical module
the diff changed; without one, or when the diff cannot be read, the whole module."""

from __future__ import annotations

from dataclasses import dataclass

from steps import changes
from steps.common import Context

NIGHTLY = "o módulo inteiro roda na mutação noturna do main"
UNTOUCHED = "nenhum arquivo do módulo no diff; nada a mutar aqui"


@dataclass(frozen=True)
class DiffScope:
    """The paths, from the root, the pull request changed (None: every file) and the line that
    tells the reader which of the two the run is."""

    paths: frozenset[str] | None
    line: str

    def keeps(self, files: tuple[str, ...]) -> tuple[str, ...]:
        """The ``files`` this run mutates: all of them, or those the diff changed."""
        return files if self.paths is None else tuple(f for f in files if f in self.paths)


def read(ctx: Context) -> DiffScope:
    """The scope of the run in ``ctx``: the diff from its base, or the whole module."""
    if not ctx.base:
        return DiffScope(None, "escopo: módulo inteiro (não é pull request)")
    found = changes.changed_paths(ctx)
    if isinstance(found, str):
        return DiffScope(None, f"escopo: módulo inteiro, pois {found}")
    return DiffScope(
        frozenset(found), f"escopo: só os arquivos do diff contra {ctx.base}; {NIGHTLY}"
    )


def mutated_notes(scope: DiffScope, chosen: dict[str, tuple[str, ...]]) -> tuple[str, ...]:
    """In a run of the diff, a line per module with code: the files mutated, or that none was."""
    if scope.paths is None:
        return ()
    return tuple(
        f"`{prefix}`: mutados {', '.join(files)}" if files else f"`{prefix}`: {UNTOUCHED}"
        for prefix, files in chosen.items()
    )
