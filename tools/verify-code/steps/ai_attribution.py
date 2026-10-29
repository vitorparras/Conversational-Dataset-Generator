"""Rejects commits whose message credits an AI assistant: a `Co-authored-by` trailer or a
"Generated with" line. Only the messages of the commits the pull request adds are read, so the
body of the pull request itself is out of reach and free to carry the line."""

from __future__ import annotations

import re
from dataclasses import dataclass

from steps.common import Context, StepResult, failed, passed, tail

NAME = "ai_attribution"
RECORD_END = "\x1e"
FIELD_SEPARATOR = "\x00"
ASSISTANTS = r"claude|anthropic|openai|chatgpt|copilot|gemini|codex|cursor|devin|aider"
CO_AUTHOR = re.compile(r"^\s*co-authored-by:\s*(?P<who>.+)$", re.IGNORECASE)
ASSISTANT = re.compile(rf"\b({ASSISTANTS})\b", re.IGNORECASE)
GENERATED_WITH = re.compile(r"^[\W_]*generated with\b", re.IGNORECASE)


@dataclass(frozen=True)
class Commit:
    """A commit as `git log` prints it: the abbreviated hash and the whole message."""

    short: str
    message: str


def parse_log(text: str) -> tuple[Commit, ...]:
    """The commits of ``git log --format=%h%x00%B%x1e``; empty records are dropped."""
    commits: list[Commit] = []
    for record in text.split(RECORD_END):
        short, _, message = record.strip().partition(FIELD_SEPARATOR)
        if short and message:
            commits.append(Commit(short, message))
    return tuple(commits)


def attributions(commit: Commit) -> tuple[str, ...]:
    """The lines of the message that credit an assistant; a human or a bot co-author passes."""
    found: list[str] = []
    for line in commit.message.splitlines():
        co_author = CO_AUTHOR.match(line)
        if (co_author and ASSISTANT.search(co_author["who"])) or GENERATED_WITH.match(line):
            found.append(line.strip())
    return tuple(found)


def run(ctx: Context) -> StepResult:
    if ctx.base is None:
        return failed(NAME, f"{NAME} exige --base <ref> (por exemplo, --base origin/main)")
    log = ["git", "log", "--format=%h%x00%B%x1e", f"{ctx.base}..HEAD"]
    outcome = ctx.run(log, ctx.root)
    if not outcome.ok:
        return failed(NAME, f"`git log {ctx.base}..HEAD` falhou", *tail(outcome.text))
    commits = parse_log(outcome.text)
    problems = [f"{c.short}: {line}" for c in commits for line in attributions(c)]
    if problems:
        return failed(
            NAME, "commit com atribuição a IA (remova a linha e reescreva a mensagem):", *problems
        )
    return passed(NAME, f"{len(commits)} commits sem atribuição a IA")
