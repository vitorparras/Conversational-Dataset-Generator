"""Simple formatting consistency rules."""

from __future__ import annotations

from pathlib import Path

from finding import Finding, Location

COLUMN_LIMIT = 100
BLANK_LINES_LIMIT = 2


def _next_fence(fence: str, content: str) -> str:
    """Returns the open fence marker after this line ("" when outside a fence)."""
    stripped = content.lstrip()
    indented_as_code = content.startswith("    ")
    marker = stripped[:3] if stripped.startswith(("```", "~~~")) and not indented_as_code else ""
    if not fence:
        return marker
    return "" if marker == fence else fence


def _line_problems(content: str, in_code: bool) -> list[str]:
    problems: list[str] = []
    if "\t" in content:
        problems.append("tab: use espaços")
    if content != content.rstrip():
        problems.append("espaço no fim da linha")
    # Tables, long links and code lines do not fit the limit without hurting reading.
    is_exception = in_code or content.startswith("    ")
    is_exception = is_exception or content.lstrip().startswith("|") or "](" in content
    if len(content) > COLUMN_LIMIT and not is_exception:
        problems.append(f"{len(content)} colunas (limite {COLUMN_LIMIT})")
    return problems


def check_formatting(root: Path, path: Path, lines: list[str]) -> list[Finding]:
    """Simple consistency rules: column, trailing space, blank lines, tab."""
    findings: list[Finding] = []
    consecutive_blank = 0
    fence = ""
    for number, line in enumerate(lines, start=1):
        content = line.rstrip("\n")
        where = Location(root, path, number)
        opened = _next_fence(fence, content)
        in_code = bool(fence or opened)
        fence = opened
        findings.extend(
            Finding.at(where, "formatting", p) for p in _line_problems(content, in_code)
        )
        consecutive_blank = 0 if content.strip() else consecutive_blank + 1
        if consecutive_blank == BLANK_LINES_LIMIT + 1:
            findings.append(Finding.at(where, "formatting", "3+ linhas vazias seguidas"))
    if lines and not lines[-1].endswith("\n"):
        message = "arquivo sem quebra de linha final"
        findings.append(Finding.at(Location(root, path, len(lines)), "formatting", message))
    return findings
