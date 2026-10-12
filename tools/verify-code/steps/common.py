"""What every step shares: the context it runs in, the result it returns, and process helpers.

Standard library only, like the rest of tools/. A step is a module with ``NAME``, ``GROUP`` and
``run(context) -> StepResult``; ``verify.py`` is the only place that lists them and fixes their order.
"""

from __future__ import annotations

import os
import shutil
import subprocess
import xml.etree.ElementTree as ET
from collections.abc import Mapping, Sequence
from dataclasses import dataclass, field
from pathlib import Path

#: The repository that contains tools/verify-code/.
DEFAULT_ROOT = Path(__file__).resolve().parents[3]

#: Where the steps leave what they measured, relative to the root. Ignored by git.
OUTPUT_DIRECTORY = ".verify-code"

#: The document that owns the critical modules and the coverage floors (issue 36).
FLOORS_DOCUMENT = "docs/operacao/014-modulos-criticos-e-pisos.md"

#: Longest time a single command may run before the step fails instead of hanging the pipeline.
COMMAND_TIMEOUT_SECONDS = 1800


@dataclass(frozen=True)
class Context:
    """Where a step runs and how strict it is."""

    root: Path
    out_dir: Path
    #: In CI a skipped test is a failure; locally it is a visible warning.
    ci: bool = False
    environment: Mapping[str, str] = field(default_factory=lambda: dict(os.environ))

    @property
    def summary_path(self) -> Path | None:
        """The file GitHub shows as the job summary, when the variable is set."""
        value = self.environment.get("GITHUB_STEP_SUMMARY")
        return Path(value) if value else None


@dataclass
class StepResult:
    """The outcome of one step: pass or fail, and everything a reader needs to act on it."""

    ok: bool = True
    lines: list[str] = field(default_factory=list)
    warnings: list[str] = field(default_factory=list)

    def fail(self, message: str) -> None:
        """Mark the step failed and say why."""
        self.ok = False
        self.lines.append(f"FALHA: {message}")

    def note(self, message: str) -> None:
        """A line of the report that is neither a failure nor a warning."""
        self.lines.append(message)

    def warn(self, message: str) -> None:
        """A visible warning: it does not fail the step."""
        self.warnings.append(message)


def find_tool(name: str) -> str | None:
    """The full path of an executable on the PATH, or None."""
    return shutil.which(name)


@dataclass(frozen=True)
class CommandOutput:
    """What a command printed, and whether it exited with 0. Truthy when it did."""

    ok: bool
    text: str = ""

    def __bool__(self) -> bool:
        return self.ok


def run_command(
    command: Sequence[str],
    cwd: Path,
    result: StepResult,
    *,
    environment: Mapping[str, str] | None = None,
    show_output_on_failure: bool = True,
) -> CommandOutput:
    """Run a command; on failure, add its tail to the result."""
    executable = find_tool(command[0])
    if executable is None:
        result.fail(f"'{command[0]}' não está no PATH; instale-o para rodar este passo")
        return CommandOutput(False)
    try:
        completed = subprocess.run(
            [executable, *command[1:]],
            cwd=cwd,
            env=dict(environment) if environment is not None else None,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=COMMAND_TIMEOUT_SECONDS,
            check=False,
        )
    except subprocess.TimeoutExpired:
        result.fail(f"`{' '.join(command)}` passou de {COMMAND_TIMEOUT_SECONDS} s")
        return CommandOutput(False)
    text = completed.stdout + completed.stderr
    if completed.returncode != 0:
        result.fail(f"`{' '.join(command)}` terminou com código {completed.returncode}")
        if show_output_on_failure:
            result.lines.extend(f"  | {line}" for line in text.strip().splitlines()[-40:])
        return CommandOutput(False, text)
    return CommandOutput(True, text)


def clean_directory(path: Path) -> None:
    """An empty directory at ``path``: results of an earlier run must never count as this run's."""
    if path.exists():
        shutil.rmtree(path)
    path.mkdir(parents=True)


def local_name(tag: str) -> str:
    """An XML tag without its namespace."""
    return tag.rsplit("}", 1)[-1]


def parse_xml(path: Path) -> ET.Element:
    """The root of an XML file written by a test tool."""
    return ET.parse(path).getroot()  # noqa: S314 - files produced by our own build, never by input


def report_skips(result: StepResult, ctx: Context, skipped: Sequence[str], suite: str) -> None:
    """Skipped tests: a failure in CI, naming each one; a visible warning on a developer machine."""
    for name in skipped:
        message = f"teste saltado em {suite}: {name}"
        if ctx.ci:
            result.fail(message + " (em CI, salto é falha)")
        else:
            result.warn(message + " (localmente é aviso; em CI reprovaria)")
