#!/usr/bin/env python3
"""Verifies the code of the repository with one command: the same steps, in the same order, as CI.

The workflow .github/workflows/verify-code.yml calls this script with the steps of each job; a test
in tools/verify-code/tests reads that file and fails if its order drifts from the one printed here.
Standard library only; the steps run `dotnet`, `uv`, `git`, `gitleaks`, `ruff` and `pyright`.
Exit code 0 only if every step that ran passed; 2 for a usage error.
"""

from __future__ import annotations

import argparse
import sys
import time
from collections.abc import Callable
from dataclasses import dataclass, replace
from pathlib import Path

from steps import (
    build_core,
    build_service,
    format_core,
    lint_service,
    lint_tools,
    pr_size,
    secrets,
    test_core,
    test_service,
    test_tools,
)
from steps.common import OUTPUT_DIRECTORY, Context, StepResult, run_process

ROOT = Path(__file__).resolve().parents[2]


@dataclass(frozen=True)
class StepSpec:
    """A step by name, the function that runs it and whether it needs a base ref."""

    name: str
    run: Callable[[Context], StepResult]
    needs_base: bool = False


STEPS: tuple[StepSpec, ...] = (
    StepSpec(build_core.NAME, build_core.run),
    StepSpec(test_core.NAME, test_core.run),
    StepSpec(format_core.NAME, format_core.run),
    StepSpec(lint_tools.NAME, lint_tools.run),
    StepSpec(test_tools.NAME, test_tools.run),
    StepSpec(build_service.NAME, build_service.run),
    StepSpec(test_service.NAME, test_service.run),
    StepSpec(lint_service.NAME, lint_service.run),
    StepSpec(secrets.NAME, secrets.run),
    StepSpec(pr_size.NAME, pr_size.run, needs_base=True),
)


def select(requested: list[str], has_base: bool) -> tuple[StepSpec, ...]:
    """The requested steps in CI order; with none requested, every step that needs no base."""
    if requested:
        return tuple(s for s in STEPS if s.name in requested)
    return tuple(s for s in STEPS if has_base or not s.needs_base)


def run_steps(specs: tuple[StepSpec, ...], ctx: Context) -> tuple[StepResult, ...]:
    """Runs every step, timing each one; a failure does not stop the ones after it."""
    results: list[StepResult] = []
    for spec in specs:
        started = time.monotonic()
        result = spec.run(ctx)
        results.append(replace(result, elapsed=time.monotonic() - started))
    return tuple(results)


def report(results: tuple[StepResult, ...]) -> int:
    """Prints one block per step and a summary line; returns the exit code."""
    for result in results:
        status = "OK   " if result.ok else "FALHA"
        print(f"{status}  {result.name:<12} {result.elapsed:7.1f} s")
        for line in result.details:
            print(f"       {line}")
    failures = [r.name for r in results if not r.ok]
    if failures:
        print(
            f"\nFALHOU — {len(failures)} de {len(results)} passos reprovaram: {', '.join(failures)}"
        )
        return 1
    print(f"\nOK — {len(results)} passos, {sum(r.elapsed for r in results):.1f} s")
    return 0


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Verifica o código do repositório.")
    parser.add_argument("--list", action="store_true", help="lista os passos, na ordem da CI")
    parser.add_argument("--step", nargs="+", default=[], metavar="NAME", help="passos a rodar")
    parser.add_argument("--base", help="ref de comparação para pr_size (ex.: origin/main)")
    args = parser.parse_args(argv)
    if args.list:
        print("\n".join(s.name for s in STEPS))
        return 0
    unknown = [n for n in args.step if n not in {s.name for s in STEPS}]
    if unknown:
        print(f"passo desconhecido: {', '.join(unknown)} (veja --list)")
        return 2
    ctx = Context(ROOT, ROOT / OUTPUT_DIRECTORY, run_process, args.base)
    try:
        return report(run_steps(select(args.step, args.base is not None), ctx))
    except Exception as error:  # last level: a defect must still print a readable line
        print(f"ERRO — defeito do verificador: {type(error).__name__}: {error}")
        return 1


if __name__ == "__main__":
    sys.exit(main())
