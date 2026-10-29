#!/usr/bin/env python3
"""Verifies the code of the repository with one command: the same steps, in the same order, as CI.

The workflow .github/workflows/verify-code.yml calls this script with the steps of each job; a test
in tools/verify-code/tests reads that file and fails if its order drifts from the one printed here.
Standard library only; the steps run `dotnet`, `uv`, `git`, `gitleaks`, `trivy`, `semgrep`, `ruff`,
`pyright` and `pip-audit`. Exit code 0 only if every step that ran passed; 2 for a usage error.
"""

from __future__ import annotations

import argparse
import io
import json
import sys
import time
from collections.abc import Callable
from dataclasses import dataclass, replace
from pathlib import Path

from steps import (
    ai_attribution,
    build_core,
    build_service,
    coverage_by_area,
    dependencies,
    floors,
    format_core,
    lint_service,
    lint_tools,
    mutation,
    package,
    pr_size,
    properties,
    ruler_proof,
    secrets,
    static_analysis,
    test_core,
    test_real_service,
    test_service,
    test_tools,
    trivy,
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
    StepSpec(test_real_service.NAME, test_real_service.run),
    StepSpec(format_core.NAME, format_core.run),
    StepSpec(lint_tools.NAME, lint_tools.run),
    StepSpec(test_tools.NAME, test_tools.run),
    StepSpec(build_service.NAME, build_service.run),
    StepSpec(test_service.NAME, test_service.run),
    StepSpec(coverage_by_area.NAME, coverage_by_area.run),
    StepSpec(floors.NAME, floors.run),
    StepSpec(lint_service.NAME, lint_service.run),
    StepSpec(properties.NAME, properties.run),
    StepSpec(package.NAME, package.run),
    StepSpec(mutation.NAME, mutation.run),
    StepSpec(ruler_proof.NAME, ruler_proof.run),
    StepSpec(dependencies.NAME, dependencies.run),
    StepSpec(secrets.NAME, secrets.run),
    StepSpec(trivy.NAME, trivy.run),
    StepSpec(static_analysis.NAME, static_analysis.run),
    StepSpec(pr_size.NAME, pr_size.run, needs_base=True),
    StepSpec(ai_attribution.NAME, ai_attribution.run, needs_base=True),
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
        started = time.perf_counter()
        result = spec.run(ctx)
        results.append(replace(result, elapsed=time.perf_counter() - started))
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


def _print_utf8() -> None:
    """Prints UTF-8 whatever the console code page: cp1252 cannot encode every reported change."""
    for stream in (sys.stdout, sys.stderr):
        if isinstance(stream, io.TextIOWrapper):
            stream.reconfigure(encoding="utf-8")


def print_mutation_modules() -> int:
    """The critical modules as a JSON list for the matrix of the CI; 1 when 014 gives none."""
    modules, problems = mutation.names(ROOT)
    if problems:
        print("\n".join(problems))
        return 1
    print(json.dumps(modules, ensure_ascii=False))
    return 0


def usage_error(requested: list[str], module: str | None) -> str | None:
    """Why the request cannot run: a step that does not exist, or a module for other steps."""
    unknown = [n for n in requested if n not in {s.name for s in STEPS}]
    if unknown:
        return f"passo desconhecido: {', '.join(unknown)} (veja --list)"
    if module and requested and mutation.NAME not in requested:
        asked = ", ".join(requested)
        return f"--module só vale para o passo {mutation.NAME}; passos pedidos: {asked}"
    return None


def main(argv: list[str] | None = None) -> int:
    _print_utf8()
    parser = argparse.ArgumentParser(description="Verifica o código do repositório.")
    parser.add_argument("--list", action="store_true", help="lista os passos, na ordem da CI")
    parser.add_argument("--step", nargs="+", default=[], metavar="NAME", help="passos a rodar")
    parser.add_argument("--module", help="módulo crítico que o passo mutation roda (um job de CI)")
    parser.add_argument(
        "--mutation-modules",
        action="store_true",
        help="imprime em JSON os módulos críticos de docs/operacao/014, a matriz do CI",
    )
    parser.add_argument("--base", help="ref de comparação para pr_size (ex.: origin/main)")
    parser.add_argument("--previous", type=Path, help="by-area.md anterior: floors avisa queda")
    args = parser.parse_args(argv)
    if args.list:
        print("\n".join(s.name for s in STEPS))
        return 0
    if args.mutation_modules:
        return print_mutation_modules()
    refusal = usage_error(args.step, args.module)
    if refusal:
        print(refusal)
        return 2
    ctx = Context(
        ROOT,
        ROOT / OUTPUT_DIRECTORY,
        run_process,
        args.base,
        args.previous,
        run_process,
        args.module,
    )
    try:
        return report(run_steps(select(args.step, args.base is not None), ctx))
    except Exception as error:  # last level: a defect must still print a readable line
        print(f"ERRO — defeito do verificador: {type(error).__name__}: {error}")
        return 1


if __name__ == "__main__":
    sys.exit(main())
