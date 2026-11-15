"""Titles of the commits a branch adds: Conventional Commits, at most 72 characters (no merges)."""

from __future__ import annotations

import re
from collections.abc import Sequence

from steps.common import Context, StepResult, failed, passed, tail

NAME = "titulos"
CEILING = 72
TYPES = "build|chore|ci|docs|feat|fix|perf|refactor|revert|style|test"
TITLE = re.compile(rf"^({TYPES})(\([a-z0-9][a-z0-9,. -]*\))?!?: \S")
SHOWN = 50


def why(title: str) -> str:
    if len(title) > CEILING:
        return f"título de {len(title)} caracteres passa do teto de {CEILING}"
    return "" if TITLE.match(title) else "fora do Conventional Commits (tipo(escopo): resumo)"


def problems(titles: Sequence[str]) -> list[str]:
    """One line per title that breaks the format or the ceiling, with the start of the title."""
    return [f"{reason}: {title[:SHOWN]}" for title in titles if (reason := why(title))]


def run(ctx: Context) -> StepResult:
    if ctx.base is None:
        return failed(NAME, "sem --base: não há commits para conferir")
    log = ctx.run(["git", "log", "--no-merges", "--format=%s", f"{ctx.base}..HEAD"], ctx.root)
    if not log.ok:
        return failed(NAME, f"`git log {ctx.base}..HEAD` falhou", *tail(log.text))
    titles = [line for line in log.text.splitlines() if line.strip()]
    if not titles:
        return failed(NAME, f"nenhum commit além de {ctx.base}: nada a conferir")
    wrong = problems(titles)
    if wrong:
        return failed(NAME, *wrong)
    return passed(NAME, f"{len(titles)} títulos conformes")
