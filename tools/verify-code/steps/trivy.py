"""Scans the repository tree with Trivy for vulnerable locks, secrets and misconfiguration.

HIGH and CRITICAL fail unless accepted-risks.toml accepts the finding within its deadline.
No source leaves the machine: the tool only downloads its databases, with telemetry and the
version check off. A secret is named by rule and place, never by value."""

from __future__ import annotations

import json
from datetime import date
from pathlib import Path
from typing import Any, cast

from steps import accepted_risks, trivy_binary
from steps.accepted_risk import Risk
from steps.common import Context, StepResult, failed, passed, reset_directory, tail
from steps.trivy_finding import Finding
from steps.trivy_section import Section

NAME = "trivy"
SCANNERS = "vuln,secret,misconfig"
SEVERITIES = "HIGH,CRITICAL"
SKIPPED = (".verify-code", "**/.venv", "**/bin", "**/obj")
QUIET = ("--disable-telemetry", "--skip-version-check", "--no-progress")
REPORT_DIRECTORY = "trivy"
REPORT_FILE = "report.json"


SECTIONS = (
    Section("Vulnerabilities", "VulnerabilityID", "PkgName", "vulnerabilidade"),
    Section("Secrets", "RuleID", "Target", "segredo"),
    Section("Misconfigurations", "ID", "Target", "configuração", status_matters=True),
)


def scan_command(binary: str, report: Path) -> list[str]:
    command = [binary, "fs", "--scanners", SCANNERS, "--severity", SEVERITIES]
    command += ["--format", "json", "--output", str(report), "--exit-code", "0", *QUIET]
    for folder in SKIPPED:
        command += ["--skip-dirs", folder]
    return [*command, "."]


def read_report(report: Path) -> dict[str, Any] | None:
    """The parsed JSON report, or None when it is missing or not an object."""
    if not report.is_file():
        return None
    try:
        data = json.loads(report.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        return None
    return cast("dict[str, Any]", data) if isinstance(data, dict) else None


def rows(container: dict[str, Any], key: str) -> list[dict[str, Any]]:
    """The list of objects under ``key``, empty when the key is absent or null."""
    return cast("list[dict[str, Any]]", container.get(key) or [])


def section_findings(result: dict[str, Any], section: Section) -> list[Finding]:
    """The failed items of one section of one scanned file."""
    target = str(result["Target"])
    return [
        Finding(
            section.kind,
            str(item[section.id_key]),
            str(item.get(section.subject_key) or target),
            target,
            str(item["Severity"]),
        )
        for item in rows(result, section.key)
        if not section.status_matters or item.get("Status") == "FAIL"
    ]


def findings_of(report: dict[str, Any]) -> tuple[Finding, ...]:
    """Every vulnerability, secret and failed misconfiguration check of every scanned file."""
    return tuple(
        finding
        for result in rows(report, "Results")
        for section in SECTIONS
        for finding in section_findings(result, section)
    )


def describe(finding: Finding) -> str:
    what = f"{finding.kind} {finding.id}  {finding.subject}"
    return f"  - {finding.severity}  {what}  ({finding.target})"


def judge(findings: tuple[Finding, ...], risks: tuple[Risk, ...]) -> StepResult:
    """Findings no accepted risk covers fail; accepted ones are counted, not hidden."""
    open_findings = [f for f in findings if not accepted_risks.covering(risks, (f.id,), f.subject)]
    accepted = len(findings) - len(open_findings)
    if open_findings:
        return failed(
            NAME,
            f"{len(open_findings)} achado(s) HIGH/CRITICAL sem risco aceito",
            *(describe(f) for f in open_findings),
        )
    return passed(NAME, f"nenhum achado HIGH/CRITICAL em aberto ({accepted} com risco aceito)")


def scan(ctx: Context, binary: str, today: date) -> StepResult:
    registry = accepted_risks.check(ctx.root, today, NAME)
    if isinstance(registry, StepResult):
        return registry
    folder = ctx.out_dir / REPORT_DIRECTORY
    reset_directory(folder)
    report = folder / REPORT_FILE
    outcome = ctx.run(scan_command(binary, report), ctx.root)
    data = read_report(report)
    if not outcome.ok or data is None:
        return failed(NAME, "o trivy falhou sem relatório de achados", *tail(outcome.text))
    return judge(findings_of(data), registry.risks)


def run(ctx: Context) -> StepResult:
    located = trivy_binary.locate()
    if located.path is None:
        return failed(NAME, located.problem)
    return scan(ctx, located.path, date.today())
