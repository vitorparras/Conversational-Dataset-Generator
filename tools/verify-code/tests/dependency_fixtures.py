"""Synthetic dependency trees for the tests of the dependencies step, and a fake of its tools.

Each tree brings a vulnerable leaf in transitively: project > direct > (middle >) leaf.
"""

from __future__ import annotations

import json
from collections.abc import Mapping, Sequence
from dataclasses import dataclass, field
from pathlib import Path

from steps import accepted_risks, service_audit
from steps.common import CommandOutcome

from . import support

PROJECT = "core/src/Zqx.App/Zqx.App.csproj"
CORE_LOCK = "core/src/Zqx.App/packages.lock.json"
ADVISORY = "https://github.com/advisories/GHSA-zqx0-0000-0001"
SOLUTION = '<Solution>\n  <Project Path="src/Zqx.App/Zqx.App.csproj" />\n</Solution>\n'
NUGET_LOCK = {
    "version": 2,
    "dependencies": {
        "net10.0": {
            "Zqx.Direct": {"type": "Direct", "dependencies": {"Zqx.Middle": "1.0.0"}},
            "Zqx.Middle": {"type": "Transitive", "dependencies": {"Zqx.Leaf": "1.0.0"}},
            "Zqx.Leaf": {"type": "Transitive"},
        }
    },
}
UV_LOCK = """version = 1

[[package]]
name = "language-service"
source = { editable = "." }
dependencies = [{ name = "zqx-web" }]

[package.dev-dependencies]
dev = [{ name = "zqx-test" }]

[[package]]
name = "zqx-web"
source = { registry = "https://pypi.org/simple" }
dependencies = [{ name = "zqx-leaf" }]

[[package]]
name = "zqx-test"
source = { registry = "https://pypi.org/simple" }

[[package]]
name = "zqx-leaf"
source = { registry = "https://pypi.org/simple" }
"""
EXPORT = (
    "zqx-web==2.0 \\\n    --hash=sha256:00\n"
    "zqx-leaf==1.0 ; sys_platform == 'win32' \\\n    --hash=sha256:01\n"
)
PYTHON_VULNERABLE: Mapping[str, object] = {
    "name": "zqx-leaf",
    "version": "1.0",
    "vulns": [
        {"id": "PYSEC-0000-1", "aliases": ["CVE-0000-0002"], "fix_versions": []},
        {"id": "PYSEC-0000-3", "aliases": ["CVE-0000-0004"], "fix_versions": []},
    ],
}


def restore_error(root: Path, code: str = "NU1903", severity: str = "high") -> str:
    """The line `dotnet restore` prints for a vulnerable package, as an audit error."""
    return (
        f"{root / PROJECT} : error {code}: Warning As Error: Package 'Zqx.Leaf' 1.0.0 has a "
        f"known {severity} severity vulnerability, {ADVISORY}\n"
    )


def suppression(advisory: str = ADVISORY) -> str:
    item = f'<NuGetAuditSuppress Include="{advisory}" />'
    return f"<Project>\n  <ItemGroup>\n    {item}\n  </ItemGroup>\n</Project>\n"


def audit_report(*dependencies: Mapping[str, object]) -> str:
    """The JSON report of pip-audit, one entry per audited package."""
    clean: Mapping[str, object] = {"name": "zqx-web", "version": "2.0", "vulns": []}
    return json.dumps({"dependencies": [clean, *dependencies]})


@dataclass
class ServiceRunner:
    """Plays `uv export` and pip-audit in the report folder; keeps the export pip-audit read."""

    folder: Path
    report: str | None
    outcome: CommandOutcome = support.SUCCESS
    commands: list[list[str]] = field(default_factory=list[list[str]])
    audited: str = ""
    restore: CommandOutcome = support.SUCCESS

    def __call__(self, command: Sequence[str], cwd: Path) -> CommandOutcome:
        self.commands.append(list(command))
        if command[0] == "dotnet":
            return self.restore
        export = self.folder / service_audit.EXPORT_FILE
        if "export" in command:
            export.write_text(EXPORT, encoding="utf-8")
        elif self.report is not None:
            self.audited = export.read_text(encoding="utf-8")
            (self.folder / service_audit.REPORT_FILE).write_text(self.report, encoding="utf-8")
        return self.outcome


def risk(identifier: str, subject: str, expires: str = "2026-12-31") -> str:
    """One `[[risk]]` table of accepted-risks.toml."""
    return (
        f'[[risk]]\nid = "{identifier}"\nsubject = "{subject}"\nexpires = {expires}\n'
        'reason = "sem versão corrigida"\n'
    )


def locked_repository(root: Path) -> Path:
    """Both locked trees and an empty register under ``root``."""
    support.write(root, "core/Core.slnx", SOLUTION)
    support.write(root, PROJECT, "<Project />\n")
    support.write(root, CORE_LOCK, json.dumps(NUGET_LOCK))
    support.write(root, "core/Directory.Packages.props", "<Project />\n")
    support.write(root, service_audit.LOCK, UV_LOCK)
    support.write(root, accepted_risks.FILE, "# nenhum risco aceito\n")
    return root


def report_folder(root: Path) -> Path:
    return root / support.OUTPUT_DIRECTORY / service_audit.REPORT_DIRECTORY
