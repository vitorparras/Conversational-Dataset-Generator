"""Scans git history with gitleaks for secrets; findings are named by rule, file and line only.

With a base ref only the commits the change adds are scanned; without one, the whole history.
The run is always redacted and the report is read from JSON: the tool's own output is never echoed,
because it carries the commit author.
"""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any, cast

from steps import gitleaks_binary
from steps.common import CommandOutcome, Context, StepResult, failed, passed, reset_directory, tail

NAME = "secrets"
CONFIG = ".gitleaks.toml"
REPORT_DIRECTORY = "gitleaks"
REPORT_FILE = "report.json"
SHORT_COMMIT = 7


def scan_command(binary: str, report: Path, base: str | None) -> list[str]:
    command = [
        binary,
        "git",
        "--redact",
        "--no-banner",
        "--no-color",
        f"--config={CONFIG}",
        "--report-format=json",
        f"--report-path={report}",
    ]
    if base is not None:
        command.append(f"--log-opts={base}..HEAD")
    return [*command, "."]


def read_findings(report: Path) -> list[dict[str, Any]] | None:
    """The findings in the JSON report, or None when it is missing or not a list."""
    if not report.is_file():
        return None
    try:
        data = json.loads(report.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        return None
    return cast("list[dict[str, Any]]", data) if isinstance(data, list) else None


def describe(finding: dict[str, Any]) -> str:
    """Rule, file, line and short commit of one finding; never the secret or the author."""
    commit = str(finding["Commit"])[:SHORT_COMMIT]
    return f"  - {finding['RuleID']}  {finding['File']}:{finding['StartLine']}  (commit {commit})"


def judge(findings: list[dict[str, Any]] | None, outcome: CommandOutcome, scope: str) -> StepResult:
    """Findings fail; an unreadable report or a failed run without findings is a tool error."""
    if findings:
        return failed(
            NAME,
            f"{len(findings)} segredo(s) {scope}; valores omitidos",
            *(describe(f) for f in findings),
        )
    if findings is None or not outcome.ok:
        return failed(NAME, "o gitleaks falhou sem relatório de achados", *tail(outcome.text))
    return passed(NAME, f"nenhum segredo {scope}")


def scan(ctx: Context, binary: str) -> StepResult:
    if not (ctx.root / CONFIG).is_file():
        return failed(NAME, f"{CONFIG} não existe na raiz: sem ele a varredura não tem as regras")
    folder = ctx.out_dir / REPORT_DIRECTORY
    reset_directory(folder)
    report = folder / REPORT_FILE
    outcome = ctx.run(scan_command(binary, report, ctx.base), ctx.root)
    scope = "no histórico inteiro" if ctx.base is None else f"nos commits de {ctx.base}..HEAD"
    return judge(read_findings(report), outcome, scope)


def run(ctx: Context) -> StepResult:
    located = gitleaks_binary.locate()
    if located.path is None:
        return failed(NAME, located.problem)
    return scan(ctx, located.path)
