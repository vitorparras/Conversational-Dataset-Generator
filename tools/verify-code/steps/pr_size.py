"""Measures the production diff from the base; tests, locks and fixtures are listed, not counted."""

from __future__ import annotations

import os
import re
from dataclasses import dataclass

from steps.common import Context, StepResult, failed, passed, tail

NAME = "pr_size"
DEFAULT_CEILING = 400
HARD_CEILING = 1500
CEILING_LINE = re.compile(
    r"^\**Teto da fatia:\**\s*(\d{1,3}(?:\.\d{3})+|\d+)\s+linhas\**\s*$", re.MULTILINE
)
CHECKED_PLAN = re.compile(r"^\**Plano conferido pelo Arquiteto:\**\s*sim\b", re.MULTILINE | re.I)
CLOSES = re.compile(r"\b(?:closes|fixes|resolves):?\s+#(\d+)", re.IGNORECASE)
HIDDEN = re.compile(r"<!--.*?-->|```.*?```", re.DOTALL)
BRANCH_ISSUE = re.compile(r"^[a-z]+/(\d+)-")
EXCLUDED_SUFFIXES = (".lock.json",)
EXCLUDED_NAMES = ("uv.lock",)
EXCLUDED_FOLDERS = ("tests", "fixtures")


@dataclass(frozen=True)
class ChangedFile:
    """One line of `git diff --numstat`: the path, the additions and the removals."""

    path: str
    added: int
    removed: int

    @property
    def lines(self) -> int:
        return self.added + self.removed


def is_excluded(path: str) -> bool:
    """Outside the count: any `tests/` or `fixtures/` folder, `*.lock.json` and `uv.lock`."""
    parts = path.split("/")
    in_excluded_folder = any(part in EXCLUDED_FOLDERS for part in parts[:-1])
    return in_excluded_folder or path.endswith(EXCLUDED_SUFFIXES) or parts[-1] in EXCLUDED_NAMES


def parse_numstat(text: str) -> tuple[ChangedFile, ...]:
    """Binary files show `-` instead of numbers and count as zero lines."""
    changed: list[ChangedFile] = []
    for line in text.splitlines():
        added, removed, path = line.split("\t", 2)
        changed.append(
            ChangedFile(
                path, int(added) if added.isdigit() else 0, int(removed) if removed.isdigit() else 0
            )
        )
    return tuple(changed)


@dataclass(frozen=True)
class Ceiling:
    """The lines a slice may change and where the number came from."""

    lines: int
    source: str
    issue: str | None = None
    unread: bool = False


def issue_number(pr_body: str, branch: str) -> str | None:
    """The issue the pull request closes: the first `Closes #n` outside comments and code blocks
    of the body, else the number of a `type/n-slug` branch."""
    closing = CLOSES.search(HIDDEN.sub("", pr_body))
    named = BRANCH_ISSUE.match(branch)
    return closing.group(1) if closing else named.group(1) if named else None


def slice_ceiling(issue_text: str) -> int | None:
    """The number of the line `Teto da fatia: N linhas`, only from an issue whose plan the
    Arquiteto checked; dots group thousands."""
    visible = HIDDEN.sub("", issue_text)
    found = CEILING_LINE.search(visible)
    checked = CHECKED_PLAN.search(visible)
    return int(found.group(1).replace(".", "")) if found and checked else None


def ceiling_for(ctx: Context) -> Ceiling:
    """The ceiling of the issue this pull request closes; the default when none is declared."""
    branch = (
        os.environ.get("GITHUB_HEAD_REF")
        or ctx.run(["git", "rev-parse", "--abbrev-ref", "HEAD"], ctx.root).text.strip()
    )
    number = issue_number(os.environ.get("CDG_PR_BODY", ""), branch)
    if number is None:
        return Ceiling(DEFAULT_CEILING, "o padrão: nenhuma issue achada")
    issue = ctx.run(["gh", "issue", "view", number, "--json", "body", "--jq", ".body"], ctx.root)
    if not issue.ok:
        return Ceiling(DEFAULT_CEILING, f"a issue #{number} não foi lida", number, unread=True)
    declared = slice_ceiling(issue.text)
    if declared is None:
        return Ceiling(DEFAULT_CEILING, f"o padrão: a issue #{number} sem teto e plano conferido")
    return Ceiling(declared, f"da issue #{number}", number)


def measure(changed: tuple[ChangedFile, ...], ceiling: Ceiling) -> StepResult:
    """The production count against the ceiling, followed by what was left out of it."""
    counted = [c for c in changed if not is_excluded(c.path)]
    excluded = [c for c in changed if is_excluded(c.path)]
    files, lines = len(counted), sum(c.lines for c in counted)
    report = [
        f"produção: {files} arquivos, {lines} linhas "
        f"(teto {ceiling.lines} linhas, {ceiling.source})",
        f"fora da conta: {len(excluded)} arquivos",
        *(f"  - {c.path} ({c.lines} linhas)" for c in excluded),
    ]
    problems: list[str] = []
    if ceiling.unread:
        problems.append(f"{ceiling.source}: o teto dela vale e não se sabe qual é")
    if ceiling.lines > HARD_CEILING:
        problems.append(
            f"o teto de {ceiling.lines} linhas da issue #{ceiling.issue} passa do limite duro "
            f"de {HARD_CEILING}"
        )
    if lines > ceiling.lines:
        problems.append(f"{lines} linhas de produção passam do teto de {ceiling.lines}")
    if problems:
        return failed(NAME, *problems, *report)
    return passed(NAME, *report)


def run(ctx: Context) -> StepResult:
    if ctx.base is None:
        return failed(NAME, "pr_size exige --base <ref> (por exemplo, --base origin/main)")
    outcome = ctx.run(["git", "diff", "--numstat", f"{ctx.base}...HEAD"], ctx.root)
    if not outcome.ok:
        return failed(NAME, f"`git diff {ctx.base}...HEAD` falhou", *tail(outcome.text))
    return measure(parse_numstat(outcome.text), ceiling_for(ctx))
