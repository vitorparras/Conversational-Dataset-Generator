#!/usr/bin/env python3
"""The check before the push: `python tools/dev/check.py [--base origin/main]`, over base..HEAD.

Titles, PR size, docs, build, format and tests without a database, one build at a time on the
machine. The report fits 40 lines (the rest in `.verify-code/check.log`); exit 0 if all pass."""

from __future__ import annotations

import argparse
import sys
import time
from collections.abc import Callable, Sequence
from functools import partial

import bootstrap
import commit_titles
import dotnet_steps
import machine_lock
import scope
from steps import pr_size
from steps.common import OUTPUT_DIRECTORY, Context, StepResult, failed, passed, run_process, tail

ROOT = bootstrap.ROOT
LOG = f"{OUTPUT_DIRECTORY}/check.log"
REPORT_LINES = 40


def docs(ctx: Context) -> StepResult:
    outcome = ctx.run([sys.executable, "tools/verify-docs/verify.py"], ctx.root)
    if outcome.ok:
        return passed("docs", "documentação conforme")
    return failed("docs", "verify-docs reprovou:", *tail(outcome.text))


def timed(step: Callable[..., StepResult], ctx: Context, *args: object) -> StepResult:
    start = time.monotonic()
    result = step(ctx, *args)
    return StepResult(result.name, result.ok, result.details, time.monotonic() - start)


def dotnet_run(ctx: Context) -> list[StepResult]:
    reach = scope.compute(ctx)
    built = timed(dotnet_steps.build, ctx, reach)
    after = {dotnet_steps.FORMAT: dotnet_steps.format_check, dotnet_steps.TESTS: dotnet_steps.tests}
    rest = [
        timed(step, ctx, reach) if built.ok else failed(name, "não rodou: o build falhou")
        for name, step in after.items()
    ]
    return [built, *rest]


def render(results: Sequence[StepResult], seconds: float) -> list[str]:
    lines: list[str] = []
    for result in results:
        first, *rest = result.details or ("",)
        lines.append(
            f"{'ok   ' if result.ok else 'FALHA'} {result.name:<8} {result.elapsed:5.1f}s  {first}"
        )
        lines += [f"        {line}" for line in rest]
    bad = [r.name for r in results if not r.ok]
    verdict = "reprovado em " + ", ".join(bad) if bad else "tudo certo para o push"
    return [*lines, f"--- {verdict} ({seconds:.0f}s)"]


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Verificação local antes do push")
    parser.add_argument("--base", default="origin/main", help="ref de comparação (origin/main)")
    base = parser.parse_args(argv).base
    out_dir = ROOT / OUTPUT_DIRECTORY
    out_dir.mkdir(exist_ok=True)
    ctx = Context(ROOT, out_dir, run_process, base)
    if not ctx.run(["git", "rev-parse", "--verify", "--quiet", f"{base}^{{commit}}"], ROOT).ok:
        print(f"a base {base} não existe: rode `git fetch origin main` ou passe --base <ref>")
        return 1
    start = time.monotonic()
    results = [timed(step, ctx) for step in (commit_titles.run, pr_size.run, docs)]
    try:
        with machine_lock.machine_lock(machine_lock.lock_path(), partial(print, flush=True)):
            results += dotnet_run(ctx)
    except TimeoutError as busy:
        results.append(failed("trava", str(busy)))
    lines = render(results, time.monotonic() - start)
    (ROOT / LOG).write_text("\n".join(lines) + "\n", encoding="utf-8")
    if len(lines) > REPORT_LINES:
        lines = [*lines[: REPORT_LINES - 2], f"... (resto em {LOG})", lines[-1]]
    print("\n".join(lines))
    return 0 if all(result.ok for result in results) else 1


if __name__ == "__main__":
    sys.exit(main())
