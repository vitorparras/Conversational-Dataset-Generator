"""Scans git history with gitleaks; a finding is named by rule, file and line, never by value."""

from __future__ import annotations

import json
import shutil
from pathlib import Path
from typing import Any, cast

from steps import gitleaks_binary
from steps.common import CommandOutcome, Context, StepResult, failed, passed, reset_directory, tail

NAME = "secrets"
CONFIG = ".gitleaks.toml"
REPORT_DIRECTORY = "gitleaks"
REPORT_FILE = "report.json"
SHORT_COMMIT = 7


def scan_command(binary: str, report: Path, limit: str | None) -> list[str]:
    command = [
        binary,
        "git",
        "--redact",
        "--no-banner",
        "--no-color",
        "--ignore-gitleaks-allow",
        f"--config={CONFIG}",
        "--report-format=json",
        f"--report-path={report}",
    ]
    if limit is not None:
        command.append(f"--log-opts={limit}")
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


def reach(ctx: Context) -> tuple[str | None, str]:
    """The ``--log-opts`` that limit the scan, and how a finding names where it looked: the commits
    of the pull request, the history that leads to HEAD (the push to main, which must not turn red
    for a branch it does not hold, #722), or every branch of the clone."""
    if ctx.base is not None:
        return f"{ctx.base}..HEAD", f"nos commits de {ctx.base}..HEAD"
    if ctx.head_only:
        return "HEAD", "no histórico de HEAD"
    return None, "no histórico inteiro"


def scan(ctx: Context, binary: str) -> StepResult:
    if not (ctx.root / CONFIG).is_file():
        return failed(NAME, f"{CONFIG} não existe na raiz: sem ele a varredura não tem as regras")
    folder = ctx.out_dir / REPORT_DIRECTORY
    reset_directory(folder)
    report = folder / REPORT_FILE
    limit, scope = reach(ctx)
    outcome = ctx.run(scan_command(binary, report, limit), ctx.root)
    findings = read_findings(report)
    shutil.rmtree(folder, ignore_errors=True)
    return judge(findings, outcome, scope)


def run(ctx: Context) -> StepResult:
    located = gitleaks_binary.locate()
    if located.path is None:
        return failed(NAME, located.problem)
    return scan(ctx, located.path)
