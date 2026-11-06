"""Decides if a pull request needs the heavy jobs of the code, from the files it changes.

Only a diff of Markdown documents (under ``docs/`` or at the root) that no code, test or tool reads
skips them; anything else, an error, an empty diff or a missing base runs everything. A document
counts as read when ``git grep`` finds its path, a folder of it or its file name in ``core/``,
``language-service/`` or ``tools/`` (a comment that cites it counts too). ``tools/verify-docs`` and
``tools/verify-code/tests`` are left out of the search: they build their own repositories.
"""

from __future__ import annotations

import re
from dataclasses import dataclass
from pathlib import PurePosixPath

from steps.common import Context

DOCS_FOLDER = "docs"
CODE_FOLDERS = ("core", "language-service", "tools")
LEFT_OUT = (":!tools/verify-docs", ":!tools/verify-code/tests")
GENERIC_NAME = "README.md"
SEPARATOR = r"[^A-Za-z0-9_.-]{1,10}"
FOLDER_END = r"""(["']|/["'{*$]|\\|$)"""
NOT_A_PATH = r"(^|[^A-Za-z0-9_./-])"
NO_MATCH = 1


@dataclass(frozen=True)
class Verdict:
    """Whether the heavy jobs must run, and the lines that say why."""

    heavy: bool
    reasons: tuple[str, ...]

    @property
    def output(self) -> str:
        return "heavy=true" if self.heavy else "heavy=false"


def heavy(*reasons: str) -> Verdict:
    return Verdict(True, reasons)


def is_documentation(path: str) -> bool:
    """A Markdown file under ``docs/`` or at the root; any other file is code or configuration."""
    pure = PurePosixPath(path)
    in_docs = pure.parts[0] == DOCS_FOLDER and len(pure.parts) > 1
    return pure.suffix == ".md" and (in_docs or len(pure.parts) == 1)


def _escaped(text: str) -> str:
    """``text`` as a literal inside an extended regular expression (a hyphen needs no escape)."""
    return re.sub(r"([.\[\]()*+?{}|^$\\])", r"\\\1", text)


def _joined(segments: tuple[str, ...]) -> str:
    return SEPARATOR.join(_escaped(segment) for segment in segments)


def reader_patterns(path: str) -> tuple[str, ...]:
    """Extended regular expressions for the ways code can name the document at ``path``: the path
    and the folders above it, with any punctuation between segments (`Path.Combine` style), a
    folder only when it ends the string or a quote or wildcard follows it, and the file name."""
    parts = PurePosixPath(path).parts
    if len(parts) == 1:
        return (NOT_A_PATH + _escaped(parts[0]),)
    patterns = [_joined(parts)]
    patterns += [_joined(parts[:depth]) + FOLDER_END for depth in range(2, len(parts))]
    if parts[-1] != GENERIC_NAME:
        patterns.append(_escaped(parts[-1]))
    return tuple(patterns)


def read_by_code(ctx: Context, path: str) -> bool:
    """True when ``git grep`` finds the document named, or it cannot tell (any error counts)."""
    command = ["git", "grep", "-q", "-E"]
    for pattern in reader_patterns(path):
        command += ["-e", pattern]
    command += ["--", *CODE_FOLDERS, *LEFT_OUT]
    outcome = ctx.run(command, ctx.root)
    return outcome.code != NO_MATCH


def changed_paths(ctx: Context) -> tuple[str, ...] | str:
    """The paths changed against the base (a rename counts both ends), or why it could not tell."""
    command = ["git", "-c", "core.quotepath=off", "diff", "--name-only", "--no-renames"]
    outcome = ctx.run([*command, f"{ctx.base}...HEAD"], ctx.root)
    if not outcome.ok:
        return f"`git diff {ctx.base}...HEAD` falhou"
    return tuple(line for line in outcome.text.splitlines() if line)


def verdict(ctx: Context) -> Verdict:
    """The decision for the diff of ``ctx.base`` to HEAD; without a base, the whole ruler."""
    if ctx.base is None:
        return heavy("sem base de comparação: não é pull request, a régua roda inteira")
    paths = changed_paths(ctx)
    if isinstance(paths, str):
        return heavy(paths)
    if not paths:
        return heavy("o diff está vazio: nada prova que dispensar é seguro")
    not_documentation = [p for p in paths if not is_documentation(p)]
    if not_documentation:
        return heavy(
            *(f"{p}: não é documento Markdown de docs/ nem da raiz" for p in not_documentation)
        )
    read = [p for p in paths if read_by_code(ctx, p)]
    if read:
        return heavy(*(f"{p}: código, teste ou ferramenta lê ou cita este documento" for p in read))
    return Verdict(False, (f"{len(paths)} documentos que nenhum código lê",))
