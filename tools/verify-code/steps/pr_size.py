"""Measures the production diff from the base; tests, locks and fixtures are listed, not counted."""

from __future__ import annotations

from dataclasses import dataclass

from steps.common import Context, StepResult, failed, passed, tail

NAME = "pr_size"
FILE_CEILING = 30
LINE_CEILING = 400
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


def measure(changed: tuple[ChangedFile, ...]) -> StepResult:
    """The production count against the ceiling, followed by what was left out of it."""
    counted = [c for c in changed if not is_excluded(c.path)]
    excluded = [c for c in changed if is_excluded(c.path)]
    files, lines = len(counted), sum(c.lines for c in counted)
    report = [
        f"produção: {files} arquivos, {lines} linhas "
        f"(teto {FILE_CEILING} arquivos, {LINE_CEILING} linhas)",
        f"fora da conta: {len(excluded)} arquivos",
        *(f"  - {c.path} ({c.lines} linhas)" for c in excluded),
    ]
    problems: list[str] = []
    if files > FILE_CEILING:
        problems.append(f"{files} arquivos de produção passam do teto de {FILE_CEILING}")
    if lines > LINE_CEILING:
        problems.append(f"{lines} linhas de produção passam do teto de {LINE_CEILING}")
    if problems:
        return failed(NAME, *problems, *report)
    return passed(NAME, *report)


def run(ctx: Context) -> StepResult:
    if ctx.base is None:
        return failed(NAME, "pr_size exige --base <ref> (por exemplo, --base origin/main)")
    outcome = ctx.run(["git", "diff", "--numstat", f"{ctx.base}...HEAD"], ctx.root)
    if not outcome.ok:
        return failed(NAME, f"`git diff {ctx.base}...HEAD` falhou", *tail(outcome.text))
    return measure(parse_numstat(outcome.text))
