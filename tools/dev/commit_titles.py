"""Titles of the commits not pushed yet (no merges): Conventional Commits, at most 72 characters."""

from __future__ import annotations

import re
from collections.abc import Sequence

from steps.common import Context, StepResult, failed, passed, tail

NAME = "titulos"
CEILING = 72
TYPES = "build|chore|ci|docs|feat|fix|perf|refactor|revert|style|test"
TITLE = re.compile(rf"^({TYPES})(\([a-z0-9][a-z0-9,. -]*\))?!?: \S")


def why(title: str) -> str:
    if len(title) > CEILING:
        return f"título de {len(title)} caracteres passa do teto de {CEILING}"
    return "" if TITLE.match(title) else "fora do Conventional Commits (tipo(escopo): resumo)"


def problems(titles: Sequence[str]) -> list[str]:
    return [f"{reason}: {title}" for title in titles if (reason := why(title))]


def run(ctx: Context) -> StepResult:
    if ctx.base is None:
        return failed(NAME, "sem --base: não há commits para conferir")
    upstream = ctx.run(["git", "rev-parse", "--verify", "--quiet", "@{upstream}"], ctx.root)
    published = [f"^{upstream.text.strip()}"] if upstream.ok else []
    command = ["git", "log", "--no-merges", "--format=%s", f"{ctx.base}..HEAD", *published]
    log = ctx.run(command, ctx.root)
    if not log.ok:
        return failed(NAME, f"`git log {ctx.base}..HEAD` falhou", *tail(log.text))
    titles = [line for line in log.text.splitlines() if line.strip()]
    if not titles:
        return passed(NAME, "nenhum commit novo desde o último push")
    if wrong := problems(titles):
        return failed(NAME, *wrong)
    return passed(NAME, f"{len(titles)} títulos conformes")
