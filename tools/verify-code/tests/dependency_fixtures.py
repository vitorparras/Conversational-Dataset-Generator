"""Synthetic dependency trees for the tests of the dependencies step, and a fake of its tools.

The service's tree brings a vulnerable leaf in transitively: project > direct > leaf.
"""

from __future__ import annotations

import json
from collections.abc import Mapping, Sequence
from dataclasses import dataclass, field
from pathlib import Path

from steps import accepted_risks, service_audit
from steps.common import CommandOutcome

from . import support

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

    def __call__(self, command: Sequence[str], cwd: Path) -> CommandOutcome:
        self.commands.append(list(command))
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
    """The service's locked tree and an empty register under ``root``."""
    support.write(root, service_audit.LOCK, UV_LOCK)
    support.write(root, accepted_risks.FILE, "# nenhum risco aceito\n")
    return root


def report_folder(root: Path) -> Path:
    return root / support.OUTPUT_DIRECTORY / service_audit.REPORT_DIRECTORY
