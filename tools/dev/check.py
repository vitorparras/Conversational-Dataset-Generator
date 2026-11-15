#!/usr/bin/env python3
"""The check of a branch before the push, in minutes and on the machine of whoever pushes.

    python tools/dev/check.py [--base origin/main]

Runs, over the diff of the base to HEAD: the titles of the commits, the size of the pull request,
the documentation, the build of the .NET projects the diff reaches (analyzers as errors), the format
of the C# files it changes and the tests without a database of the reached projects. One build at a
time on the machine (`maquina.lock`). The report fits 40 lines; the whole output goes to
`.verify-code/check.log`. Exit code 0 only when every step passes. Tests with a database and the
browser stay with the CI.
"""

from __future__ import annotations

import argparse
import sys
import time
from collections.abc import Callable, Sequence

import bootstrap
import commit_titles
import docs_step
import dotnet_steps
import machine_lock
import scope
from steps import pr_size
from steps.common import OUTPUT_DIRECTORY, Context, StepResult, failed, run_process

ROOT = bootstrap.ROOT
DEFAULT_BASE = "origin/main"
LOG_NAME = "check.log"
REPORT_LINES = 40
Step = Callable[[Context], StepResult]


def timed(step: Step, ctx: Context) -> StepResult:
    """Runs a step and records how long it took."""
    start = time.monotonic()
    result = step(ctx)
    return StepResult(result.name, result.ok, result.details, time.monotonic() - start)


def dotnet_run(ctx: Context) -> list[StepResult]:
    """Build, format and tests; tests need the build, so a broken build stops them."""
    reach = scope.compute(ctx)
    header = f"{len(reach.reached)} projetos alcançados" + (
        " (diff geral)" if reach.everything else ""
    )
    results = [timed(lambda c: dotnet_steps.build(c, reach), ctx)]
    results.append(timed(lambda c: dotnet_steps.format_check(c, reach), ctx))
    if results[0].ok:
        results.append(timed(lambda c: dotnet_steps.tests(c, reach), ctx))
    else:
        results.append(failed(dotnet_steps.TESTS, "não rodaram: o build falhou"))
    return [StepResult("escopo", True, (header,)), *results]


def render(results: Sequence[StepResult]) -> list[str]:
    """One line per step and, under a failed one, the lines that say what to fix."""
    lines: list[str] = []
    for result in results:
        mark = "ok   " if result.ok else "FALHA"
        first, *rest = result.details or ("",)
        lines.append(f"{mark} {result.name:<8} {result.elapsed:5.1f}s  {first}")
        lines += [f"        {line}" for line in rest]
    return lines


def summary(results: Sequence[StepResult], seconds: float) -> list[str]:
    bad = [r.name for r in results if not r.ok]
    verdict = "reprovado em " + ", ".join(bad) if bad else "tudo certo para o push"
    return [f"--- {verdict} ({seconds:.0f}s)"]


def report(results: Sequence[StepResult], seconds: float, log: str) -> list[str]:
    """The lines to print: all of them when they fit, else the head and a pointer to the log."""
    lines = render(results)
    last = summary(results, seconds)
    if len(lines) + len(last) > REPORT_LINES:
        lines = [*lines[: REPORT_LINES - len(last) - 1], f"... (resto em {log})"]
    return [*lines, *last]


def parse(argv: Sequence[str] | None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Verificação local antes do push")
    parser.add_argument("--base", default=DEFAULT_BASE, help="ref de comparação (origin/main)")
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = parse(argv)
    out_dir = ROOT / OUTPUT_DIRECTORY
    out_dir.mkdir(exist_ok=True)
    ctx = Context(ROOT, out_dir, run_process, args.base)
    start = time.monotonic()
    results = [timed(step, ctx) for step in (commit_titles.run, pr_size.run, docs_step.run)]
    with machine_lock.machine_lock(machine_lock.lock_path(), lambda text: print(text, flush=True)):
        results += dotnet_run(ctx)
    log = out_dir / LOG_NAME
    log.write_text("\n".join(render(results)) + "\n", encoding="utf-8")
    print("\n".join(report(results, time.monotonic() - start, f"{OUTPUT_DIRECTORY}/{LOG_NAME}")))
    return 0 if all(result.ok for result in results) else 1


if __name__ == "__main__":
    sys.exit(main())
