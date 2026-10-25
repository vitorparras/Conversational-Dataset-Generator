"""Audits the language service's locked tree with pip-audit over what `uv export` writes from it.

The export pins the whole tree, transitive and dev included, with hashes, so pip-audit resolves
nothing. Platform markers are dropped, so a package locked only for another platform is audited
too. A finding passes only with an accepted risk on the package as pip-audit names it, by any id
the vulnerability goes by."""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path
from typing import Any, cast

from steps import accepted_risks, dependency_tree
from steps.accepted_risk import Risk
from steps.build_service import SERVICE
from steps.common import Context, StepResult, failed, passed, reset_directory, tail
from steps.vulnerability import Vulnerability, describe

NAME = "service_audit"
LOCK = f"{SERVICE}/uv.lock"
REQUIREMENTS = "tools/verify-code/audit-requirements.txt"
REPORT_DIRECTORY = "dependencies"
EXPORT_FILE = "requirements.txt"
REPORT_FILE = "pip-audit.json"
MARKER = re.compile(r" ; [^\\\n]*")


def export_command(target: Path) -> list[str]:
    tree = ["--all-groups", "--all-extras", "--no-emit-project", "--format", "requirements-txt"]
    where = ["--directory", SERVICE, "--output-file", str(target), "--quiet"]
    return ["uv", "export", "--locked", *tree, *where]


def audit_command(requirements: Path, report: Path) -> list[str]:
    """Hashed requirements without pip; --strict fails on a package it could not audit."""
    reading = ["--requirement", str(requirements), "--disable-pip", "--require-hashes", "--strict"]
    output = ["--progress-spinner", "off", "--format", "json", "--output", str(report)]
    return [sys.executable, "-m", "pip_audit", *reading, *output]


def read_report(report: Path) -> list[dict[str, Any]] | None:
    """The audited dependencies of the JSON report, or None when it is missing or malformed."""
    try:
        data: object = json.loads(report.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return None
    if not isinstance(data, dict):
        return None
    dependencies = cast("dict[str, object]", data).get("dependencies")
    return cast("list[dict[str, Any]]", dependencies) if isinstance(dependencies, list) else None


def findings(dependencies: list[dict[str, Any]], lock: str) -> tuple[Vulnerability, ...]:
    """Every reported vulnerability, once, with its path from the service in ``lock``."""
    tree = dependency_tree.from_uv_lock(lock)
    return tuple(
        dict.fromkeys(
            Vulnerability(
                str(d["name"]),
                str(d["version"]),
                (str(v["id"]), *sorted(map(str, v.get("aliases", [])))),
                dependency_tree.path_to(tree, str(d["name"])),
            )
            for d in dependencies
            for v in d.get("vulns", [])
        )
    )


def judge(found: tuple[Vulnerability, ...], risks: tuple[Risk, ...]) -> StepResult:
    """Findings no accepted risk covers fail; the accepted ones are listed, never hidden."""
    covered = {v: accepted_risks.covering(risks, v.ids, v.package) for v in found}
    unaccepted = [v for v, risk in covered.items() if risk is None]
    if unaccepted:
        heading = f"serviço: {len(unaccepted)} vulnerabilidade(s) sem risco aceito"
        return failed(NAME, heading, *(describe(v) for v in unaccepted))
    used = dict.fromkeys(risk for risk in covered.values() if risk is not None)
    lines = (accepted_risks.accepted_line(r) for r in used)
    return passed(NAME, "serviço: árvore completa auditada, nada em aberto", *lines)


def audit(ctx: Context, risks: tuple[Risk, ...]) -> StepResult:
    if not (ctx.root / LOCK).is_file():
        return failed(NAME, f"{LOCK} não existe: rode `uv lock` em {SERVICE}/ e versione o lock")
    folder = ctx.out_dir / REPORT_DIRECTORY
    reset_directory(folder)
    exported = ctx.run(export_command(folder / EXPORT_FILE), ctx.root)
    if not exported.ok:
        cause = "serviço: `uv export` falhou; não há árvore a auditar"
        return failed(NAME, cause, *tail(exported.text))
    export = folder / EXPORT_FILE
    export.write_text(MARKER.sub("", export.read_text(encoding="utf-8")), encoding="utf-8")
    outcome = ctx.run(audit_command(export, folder / REPORT_FILE), ctx.root)
    dependencies = read_report(folder / REPORT_FILE)
    if dependencies is None:
        heading = "serviço: o pip-audit falhou sem relatório (ferramenta ou rede)"
        hint = f"instale no Python do verify.py: `python -m pip install -r {REQUIREMENTS}`"
        return failed(NAME, heading, hint, *tail(outcome.text))
    if not dependencies:
        return failed(NAME, "serviço: o pip-audit não auditou nenhum pacote", *tail(outcome.text))
    lock = (ctx.root / LOCK).read_text(encoding="utf-8")
    return judge(findings(dependencies, lock), risks)
