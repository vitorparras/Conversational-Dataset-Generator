"""Run the unit tests of tools/ under coverage.py, so the tools are an area with a measured number."""

from __future__ import annotations

import re

from steps.common import Context, StepResult, clean_directory, report_skips, run_command

NAME = "test_tools"
GROUP = "service"
DESCRIPTION = "roda os testes unitários de tools/ com cobertura"

RESULTS = "tools"
COVERAGE_FILE = "coverage.xml"


def evaluate(ctx: Context, output: str, result: StepResult) -> None:
    """The unittest summary line is the only count there is: zero tests is a failure."""
    found = re.search(r"Ran (\d+) tests?", output)
    if not found or int(found.group(1)) == 0:
        result.fail("nenhum teste de tools/ executado: o passo não passa vazio")
        return
    result.note(f"{found.group(1)} testes de tools/ executados")
    skipped = re.search(r"skipped=(\d+)", output)
    if skipped:
        report_skips(result, ctx, [f"{skipped.group(1)} teste(s) em tools/"], "tools")


def run(ctx: Context) -> StepResult:
    result = StepResult()
    tools = ctx.root / "tools"
    project = ctx.root / "language-service"
    if not tools.is_dir() or not (project / "pyproject.toml").is_file():
        result.fail("tools/ ou language-service/pyproject.toml não existe: não há como medir")
        return result
    results = ctx.out_dir / RESULTS
    clean_directory(results)
    data, xml = results / "tools.coverage", results / COVERAGE_FILE
    base = ["uv", "run", "--frozen", "--directory", str(project), "python", "-m", "coverage"]
    discover = ["-m", "unittest", "discover", "-s", str(tools), "-p", "test_*.py"]
    ran = run_command(
        [*base, "run", "--branch", f"--data-file={data}", f"--source={tools}", "--omit=*/tests/*", *discover],
        ctx.root,
        result,
    )
    if ran:
        evaluate(ctx, ran.text, result)
        run_command([*base, "xml", f"--data-file={data}", "-o", str(xml)], ctx.root, result)
    return result
