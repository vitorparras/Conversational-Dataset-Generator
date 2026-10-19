"""A synthetic repository for the static analysis step and a runner that answers each semgrep call.

The runner answers `--version`, `--test` and `scan` with their own outcome and writes the scan
report where the command asks, so a test states only what semgrep would have said.
"""

from __future__ import annotations

import json
from collections.abc import Sequence
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from steps import static_analysis
from steps.common import OUTPUT_DIRECTORY, CommandOutcome, Context

from . import support

VERSION = "1.179.0"
SECRET_RULE = "secret-literal-python"
PREFIX = "tools.verify-code.semgrep."
SOURCES = {
    "core/src/Zqx/Sample.cs": "internal static class Sample\n{\n}\n",
    "language-service/src/zqx/sample.py": "x = 1\ny = 2\nz = 3\n",
    "tools/zqx/sample.py": "x = 1\ny = 2\nz = 3\n",
}


def repository(root: Path) -> Path:
    """The requirements pin, one rule file and one source file in each part."""
    support.write(root, static_analysis.REQUIREMENTS, f"semgrep=={VERSION}\n")
    support.write(root, f"{static_analysis.RULES}/secret.yml", "rules:\n  - id: zqx\n")
    for name, content in SOURCES.items():
        support.write(root, name, content)
    return root


def rule_report(passed: Sequence[str] = static_analysis.REQUIRED_RULES, **extra: Any) -> str:
    """The JSON of `semgrep --test` where every rule in ``passed`` caught exactly its examples."""
    checks: dict[str, Any] = {rule: {"passed": True} for rule in passed}
    checks.update(extra.pop("checks", {}))
    data: dict[str, Any] = {"config_missing_tests": [], "config_with_errors": [], **extra}
    data["results"] = {f"{static_analysis.RULES}/secret.yml": {"checks": checks}}
    return json.dumps(data)


def result(path: str, line: int, rule: str = SECRET_RULE) -> dict[str, Any]:
    return {"check_id": PREFIX + rule, "path": path, "start": {"line": line, "col": 1}}


def partial(path: str, first: int, last: int) -> dict[str, Any]:
    span = {"path": path, "start": {"line": first}, "end": {"line": last}}
    return {"type": ["PartialParsing", [span]], "path": path, "level": "warn"}


def scan_report(
    results: Sequence[dict[str, Any]] = (),
    errors: Sequence[dict[str, Any]] = (),
    scanned: Sequence[str] = tuple(SOURCES),
) -> str:
    data = {"results": list(results), "errors": list(errors), "paths": {"scanned": list(scanned)}}
    return json.dumps(data)


PINNED = CommandOutcome(True, f"{VERSION}\n")
LOADED_RULE = "{ Rule.id = (zqx, _); paths = None }\n"
ALL_RULES_OPEN = CommandOutcome(True, LOADED_RULE * len(static_analysis.REQUIRED_RULES))
ALL_RULES_PASS = CommandOutcome(True, rule_report())
CLEAN_SCAN = scan_report()


@dataclass
class SemgrepRunner:
    """Answers the three semgrep calls of the step; records every command it receives."""

    version: CommandOutcome = PINNED
    rules: CommandOutcome = ALL_RULES_PASS
    dump: CommandOutcome = ALL_RULES_OPEN
    report: str | None = CLEAN_SCAN
    scan: CommandOutcome = support.SUCCESS
    commands: list[list[str]] = field(default_factory=list[list[str]])

    def __call__(self, command: Sequence[str], cwd: Path) -> CommandOutcome:
        self.commands.append(list(command))
        if "--version" in command:
            return self.version
        if "--test" in command:
            return self.rules
        if "dump-config" in command:
            return self.dump
        if self.report is not None:
            output = Path(command[command.index("--output") + 1])
            output.write_text(self.report, encoding="utf-8")
        return self.scan


def context_of(root: Path, runner: SemgrepRunner) -> Context:
    return Context(root, root / OUTPUT_DIRECTORY, runner)
