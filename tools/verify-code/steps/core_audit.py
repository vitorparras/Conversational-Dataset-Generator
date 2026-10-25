"""Restores the core in locked mode with NuGet's audit over the whole tree, transitive included.

Every audit warning is an error, so an unreachable vulnerability source fails instead of passing
unaudited. A finding passes only through `NuGetAuditSuppress`, and each suppression needs an
accepted risk whose id is the suppressed advisory."""

from __future__ import annotations

import re
from pathlib import Path

from steps import accepted_risks, dependency_tree
from steps.accepted_risk import Risk
from steps.build_core import SOLUTION
from steps.common import Context, StepResult, failed, passed, tail
from steps.vulnerability import Vulnerability, describe

NAME = "core_audit"
CORE = "core"
LOCK = "packages.lock.json"
AUDIT_CODES = ("NU1900", "NU1901", "NU1902", "NU1903", "NU1904", "NU1905")
MSBUILD_SUFFIXES = (".props", ".targets", ".csproj")
SUPPRESS_ITEM = "NuGetAuditSuppress"
SUPPRESSION = re.compile(rf'<{SUPPRESS_ITEM}\s+Include="([^"]+)"\s*/>')
FINDING = re.compile(
    r"^\s*(?P<project>.+?\.csproj) : error NU190[1-4]: .*?'(?P<package>[^']+)' "
    r"(?P<version>\S+) .*?(?P<advisory>https?://\S+)",
    re.MULTILINE,
)


def restore_command() -> list[str]:
    """Locked, forced so a cached result never skips the audit, with the audit codes as errors."""
    audit = ["-p:NuGetAudit=true", "-p:NuGetAuditMode=all", "-p:NuGetAuditLevel=low"]
    warnings = ["-p:NoWarn=", "-p:TreatWarningsAsErrors=false"]
    errors = f"-p:WarningsAsErrors={'%3B'.join(AUDIT_CODES)}"
    return ["dotnet", "restore", SOLUTION, "--locked-mode", "--force", *audit, *warnings, errors]


def projects(root: Path) -> tuple[Path, ...]:
    solution = root / SOLUTION
    paths = re.findall(r'Path="([^"]+\.csproj)"', solution.read_text(encoding="utf-8"))
    return tuple(solution.parent / path.replace("\\", "/") for path in paths)


def missing_trees(root: Path) -> list[str]:
    """Why the core has no complete locked tree to audit; empty when every project has a lock."""
    if not (root / SOLUTION).is_file():
        return [f"{SOLUTION} não existe: não há árvore do núcleo a auditar"]
    found = projects(root)
    if not found:
        return [f"{SOLUTION} não lista projeto: não há árvore do núcleo a auditar"]
    return [
        f"{p.parent.relative_to(root).as_posix()}/{LOCK} não existe: rode `dotnet restore "
        f"{SOLUTION}` e versione o lock"
        for p in found
        if not (p.parent / LOCK).is_file()
    ]


def path_in_lock(project: Path, package: str) -> tuple[str, ...]:
    lock = project.parent / LOCK
    if not lock.is_file():
        return ()
    tree = dependency_tree.from_nuget_lock(lock.read_text(encoding="utf-8"))
    return (project.stem, *dependency_tree.path_to(tree, package))


def findings(output: str) -> tuple[Vulnerability, ...]:
    """Every vulnerability the restore reported, once, with its path inside the project's lock."""
    return tuple(
        dict.fromkeys(
            Vulnerability(
                m["package"],
                m["version"],
                (m["advisory"],),
                path_in_lock(Path(m["project"]), m["package"]),
            )
            for m in FINDING.finditer(output)
        )
    )


def _locked_packages(root: Path) -> tuple[str, ...]:
    """Every package name in the core's lock files."""
    locks = [p.parent / LOCK for p in projects(root) if (p.parent / LOCK).is_file()]
    trees = [dependency_tree.from_nuget_lock(lock.read_text(encoding="utf-8")) for lock in locks]
    return tuple(dict.fromkeys(name for tree in trees for name in tree.edges))


def _suppressions(root: Path) -> tuple[tuple[str, ...], list[str]]:
    """The advisories suppressed in the core's MSBuild files; a problem for any other silencing."""
    files = [p for p in (root / CORE).rglob("*") if p.suffix in MSBUILD_SUFFIXES]
    outputs = {"bin", "obj"}
    texts = [
        p.read_text(encoding="utf-8") for p in files if not outputs & set(p.relative_to(root).parts)
    ]
    advisories = tuple(url for text in texts for url in SUPPRESSION.findall(text))
    if any(code in text for text in texts for code in AUDIT_CODES):
        return advisories, [f"código NU190x calado em {CORE}/: aceite o risco com {SUPPRESS_ITEM}"]
    written = sum(text.count(SUPPRESS_ITEM) for text in texts)
    if written != len(advisories):
        return advisories, [f'{SUPPRESS_ITEM} fora da forma <{SUPPRESS_ITEM} Include="url" />']
    return advisories, []


def _accepted(risks: tuple[Risk, ...], advisory: str, packages: tuple[str, ...]) -> Risk | None:
    """The accepted risk on ``advisory`` whose subject is a package of the core's locks."""
    covering = accepted_risks.covering
    return next((r for name in packages if (r := covering(risks, (advisory,), name))), None)


def _judge_suppressions(root: Path, risks: tuple[Risk, ...]) -> tuple[list[str], list[str]]:
    """The suppressions no accepted risk on a locked package covers, and the accepted ones."""
    advisories, problems = _suppressions(root)
    packages = _locked_packages(root)
    found = {advisory: _accepted(risks, advisory, packages) for advisory in advisories}
    unaccepted = [advisory for advisory, risk in found.items() if risk is None]
    problems += [f"{SUPPRESS_ITEM} {a} sem risco aceito em pacote do núcleo" for a in unaccepted]
    used = dict.fromkeys(risk for risk in found.values() if risk is not None)
    return problems, [accepted_risks.accepted_line(risk) for risk in used]


def audit(ctx: Context, risks: tuple[Risk, ...]) -> StepResult:
    absent = missing_trees(ctx.root)
    if absent:
        return failed(NAME, *absent)
    problems, accepted = _judge_suppressions(ctx.root, risks)
    outcome = ctx.run(restore_command(), ctx.root)
    found = findings(outcome.text)
    if found:
        heading = f"núcleo: {len(found)} vulnerabilidade(s) sem risco aceito"
        return failed(NAME, heading, *(describe(v) for v in found), *problems)
    if not outcome.ok:
        cause = "lock desatualizado, pacote ou base de vulnerabilidades inacessível"
        return failed(NAME, f"núcleo: restauração auditada falhou ({cause})", *tail(outcome.text))
    if problems:
        return failed(NAME, *problems)
    return passed(NAME, "núcleo: árvore completa auditada, nada em aberto", *accepted)
