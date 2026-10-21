"""Runs the unit tests of tools/, under coverage.py when installed; zero tests is a failure."""

from __future__ import annotations

import importlib.util
import re
import sys
from pathlib import Path

from steps.common import (
    OUTPUT_DIRECTORY,
    Context,
    StepResult,
    failed,
    passed,
    reset_directory,
    tail,
)
from steps.lint_tools import REQUIREMENTS

NAME = "test_tools"
TOOLS = "tools"
RESULTS = "tools"
DATA = ".coverage"
REPORT = "coverage.xml"
UNITTEST = ("-m", "unittest", "discover", "-s", TOOLS, "-p", "test_*.py")


def commands(results: Path, measured: bool) -> tuple[list[str], ...]:
    """The test run, and with coverage the XML report of it, both writing into ``results``."""
    if not measured:
        return ([sys.executable, *UNITTEST],)
    coverage, data = [sys.executable, "-m", "coverage"], str(results / DATA)
    scope = ("--source", TOOLS, "--omit", "*/tests/*")
    return (
        [*coverage, "run", "--data-file", data, *scope, *UNITTEST],
        [*coverage, "xml", "--data-file", data, "-o", str(results / REPORT)],
    )


def run_suite(ctx: Context, measured: bool) -> StepResult:
    """Runs the suite, measured or not, and judges the count, the outcome and the report."""
    results = ctx.out_dir / RESULTS
    reset_directory(results)
    suite, *report = commands(results, measured)
    outcome = ctx.run(suite, ctx.root)
    ran = re.search(r"^Ran (\d+) tests?", outcome.text, re.MULTILINE)
    if ran is None or int(ran.group(1)) == 0:
        return failed(
            NAME,
            f"nenhum teste encontrado em {TOOLS}/: o passo não passa vazio",
            *tail(outcome.text),
        )
    if not outcome.ok:
        return failed(NAME, f"testes de {TOOLS}/ falharam", *tail(outcome.text))
    count = f"{ran.group(1)} testes de {TOOLS}/ executados"
    if not report:
        return passed(NAME, count, f"cobertura não medida: instale `pip install -r {REQUIREMENTS}`")
    xml = ctx.run(report[0], ctx.root)
    if not (results / REPORT).is_file():
        return failed(NAME, "nenhum relatório de cobertura XML foi escrito", *tail(xml.text))
    return passed(NAME, count, f"cobertura em {OUTPUT_DIRECTORY}/{RESULTS}/{REPORT}")


def run(ctx: Context) -> StepResult:
    if not (ctx.root / TOOLS).is_dir():
        return failed(NAME, f"{TOOLS}/ não existe: não há suíte para rodar")
    return run_suite(ctx, importlib.util.find_spec("coverage") is not None)
