"""Scans the core, the service and the tools with the repository's own semgrep rules, offline.

`semgrep --test` first proves every rule against its marked examples, so a rule removed, broken or
narrowed fails; then an open finding, a part with no file analyzed or an unreadable file fails.
"""

from __future__ import annotations

import re
from pathlib import Path
from typing import Any

from steps import semgrep_report, semgrep_suppression
from steps.common import Context, StepResult, failed, passed, reset_directory, tail
from steps.semgrep_report import Finding, describe, fatal

NAME = "static_analysis"
TOOL = "semgrep"
REQUIREMENTS = "tools/verify-code/static-analysis-requirements.txt"
RULES = "tools/verify-code/semgrep"
EXAMPLES = "tools/verify-code/tests/semgrep"
PARTS = {"core": ".cs", "language-service": ".py", "tools": ".py"}
FAMILIES = ("secret-literal", "sql-concatenation")
REQUIRED_RULES = tuple(f"{family}-{lang}" for family in FAMILIES for lang in ("python", "csharp"))
QUIET = ("--metrics=off", "--disable-version-check", "--quiet")
RULE_TESTS = f"{TOOL} --test --config {RULES} {EXAMPLES}"
DISPENSING = "não sendo defeito: `nosemgrep: <regra> <motivo>` na linha ou acima, fora de core/src"


def pinned_version(root: Path) -> str:
    """The version after `semgrep==` in the requirements file; empty when there is none."""
    path = root / REQUIREMENTS
    text = path.read_text(encoding="utf-8") if path.is_file() else ""
    found = re.search(r"^semgrep==(\S+)$", text, re.MULTILINE)
    return found.group(1) if found else ""


def check_tool(ctx: Context) -> list[str]:
    """The pinned semgrep must answer `--version` with exactly the pinned version."""
    expected = pinned_version(ctx.root)
    if not expected:
        return [f"{REQUIREMENTS} não fixa a versão do semgrep (`semgrep==<versão>`)"]
    outcome = ctx.run([TOOL, "--version", "--disable-version-check"], ctx.root)
    found = outcome.text.strip() if outcome.ok else "nenhum"
    install = f"instale com `pip install -r {REQUIREMENTS}`"
    problem = f"semgrep {expected} não encontrado (há: {found}); {install}"
    return [] if found == expected else [problem]


def rule_problems(results: dict[str, Any]) -> list[str]:
    """Rules without examples or with errors, rules that miss their examples, rules gone."""
    configs: dict[str, Any] = results.get("results") or {}
    checks = {rule: c for config in configs.values() for rule, c in config["checks"].items()}
    missed = [rule for rule, check in checks.items() if not check["passed"]]
    return [
        *(f"regra sem exemplos em {EXAMPLES}/: {c}" for c in results["config_missing_tests"]),
        *(f"arquivo de regras com erro: {c}" for c in results["config_with_errors"]),
        *(f"a regra {r} não acusa só as linhas marcadas nos exemplos" for r in missed),
        *(f"regra {r} ausente ou desligada em {RULES}/" for r in REQUIRED_RULES if r not in checks),
    ]


def narrowed(ctx: Context) -> list[str]:
    """Semgrep's own reading of the rule folder: every rule it loads has no `paths:`."""
    dump = ctx.run([TOOL, "show", "dump-config", RULES], ctx.root).text
    loaded = dump.count("Rule.id =")
    unrestricted = loaded >= len(REQUIRED_RULES) and dump.count("paths = None") == loaded
    return [] if unrestricted else [f"regra de {RULES}/ com `paths:` ou que o semgrep não carrega"]


def check_rules(ctx: Context) -> list[str]:
    """The rule files exist, none narrows its reach, and `semgrep --test` proves each one."""
    folder = ctx.root / RULES
    if not (folder.is_dir() and any(folder.glob("*.yml"))):
        return [f"conjunto de regras ausente: nenhum .yml em {RULES}/"]
    narrowed_rules = narrowed(ctx)
    command = [TOOL, "--test", "--json", "--config", RULES, EXAMPLES, *QUIET]
    outcome = ctx.run(command, ctx.root)
    results = semgrep_report.parse(outcome.text)
    if results is None or "config_missing_tests" not in results:
        return [*narrowed_rules, f"`{RULE_TESTS}` falhou sem relatório; rode-o para ver o motivo"]
    return [*narrowed_rules, *rule_problems(results)]


def scan_command(report: Path) -> list[str]:
    """Every file of the parts git does not ignore; nosemgrep comments are judged by the step."""
    command = [TOOL, "scan", "--config", RULES, "--json", "--output", str(report)]
    command += ["--disable-nosem", "--x-ignore-semgrepignore-files", "--max-target-bytes=0"]
    command += ["--exclude", EXAMPLES]
    return [*command, *QUIET, *PARTS]


def missing_parts(paths: tuple[str, ...]) -> list[str]:
    return [
        f"parte {part}/ sem analisador: nenhum arquivo {suffix} analisado"
        for part, suffix in PARTS.items()
        if not any(p.startswith(f"{part}/") and p.endswith(suffix) for p in paths)
    ]


def verdict(root: Path, finding: Finding) -> str:
    """The report line of a finding left open; empty when a comment validly dispenses it."""
    lines = (root / finding.path).read_text(encoding="utf-8", errors="replace").splitlines()
    comment = semgrep_suppression.comment_for(lines, finding)
    if comment is None:
        return f"{finding.path}:{finding.line}: {finding.rule}"
    return semgrep_suppression.problem_of(comment, finding)


def judge(root: Path, report: dict[str, Any]) -> StepResult:
    """Parts without analysis, unreadable files and open findings fail; partial spans are listed."""
    found = semgrep_report.findings(report)
    errors = semgrep_report.items(report, "errors")
    unreadable = [describe(e, root) for e in errors if fatal(e, root)]
    partial = [describe(e, root) for e in errors if not fatal(e, root)]
    paths = semgrep_report.scanned(report)
    open_findings = [line for line in (verdict(root, f) for f in found) if line]
    problems = [*missing_parts(paths), *unreadable, *open_findings]
    if problems:
        hint = [DISPENSING] if open_findings else []
        return failed(NAME, f"{len(problems)} problema(s) na análise estática", *problems, *hint)
    summary = f"{len(paths)} arquivos de {', '.join(PARTS)} analisados, nenhum achado em aberto"
    return passed(NAME, f"{summary} ({len(found)} dispensado(s) no ponto)", *partial)


def scan(ctx: Context) -> StepResult:
    folder = ctx.out_dir / NAME
    reset_directory(folder)
    report_path = folder / "report.json"
    outcome = ctx.run(scan_command(report_path), ctx.root)
    report = semgrep_report.read(report_path)
    if not outcome.ok or report is None:
        problem = f"o semgrep falhou sem relatório; confira se existem {', '.join(PARTS)}"
        return failed(NAME, problem, *tail(outcome.text))
    return judge(ctx.root, report)


def run(ctx: Context) -> StepResult:
    problems = check_tool(ctx) or check_rules(ctx)
    if problems:
        return failed(NAME, *problems)
    return scan(ctx)
