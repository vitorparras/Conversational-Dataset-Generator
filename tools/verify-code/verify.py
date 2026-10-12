#!/usr/bin/env python3
"""Verifies the code of the repository: build and test of the core, the service and the tools.

One command, the same steps in the same order on a developer machine and in CI. The workflow
.github/workflows/verify-code.yml calls this script once per step, in the order printed by --list.

Usage:
    python tools/verify-code/verify.py                      # every step, in order
    python tools/verify-code/verify.py --list               # names of the steps, in order
    python tools/verify-code/verify.py --step build_core    # one step (repeatable, or comma separated)
    python tools/verify-code/verify.py --ci                 # a skipped test is a failure

On Windows use `py -3 -X utf8 tools/verify-code/verify.py`. Needs `dotnet` and `uv` on the PATH for
the steps that build and test; the script itself uses the standard library only.

Exit code: 0 only if every step that ran passed. A step that executes no test fails.
"""

from __future__ import annotations

import argparse
import importlib
import os
import sys
import time
from dataclasses import dataclass
from pathlib import Path
from types import ModuleType

HERE = Path(__file__).resolve().parent
if str(HERE) not in sys.path:
    sys.path.insert(0, str(HERE))

from steps.common import DEFAULT_ROOT, OUTPUT_DIRECTORY, Context, StepResult  # noqa: E402

#: The steps, in the order they run locally and in CI. The workflow lists them in this same order.
STEP_ORDER = (
    "build_core",
    "test_core",
    "build_service",
    "test_service",
    "test_tools",
)

#: What a step reads that another step wrote. In a full run, a step whose input failed is not run.
REQUIRES = {
    "test_core": ("build_core",),
    "test_service": ("build_service",),
    "test_tools": ("build_service",),
}


@dataclass(frozen=True)
class Outcome:
    """What happened to one step of the run."""

    name: str
    status: str  # "passou" | "FALHOU" | "não rodou"
    seconds: float


def load_step(name: str) -> ModuleType:
    """The module of a step."""
    return importlib.import_module(f"steps.{name}")


def run_step(name: str, ctx: Context) -> tuple[StepResult, float]:
    """Run one step and print its report as it finishes."""
    module = load_step(name)
    print(f"\n== {name}: {module.DESCRIPTION}", flush=True)
    started = time.perf_counter()
    try:
        result = module.run(ctx)
    except Exception as error:  # a step that crashes is a failed step, with the reason shown
        result = StepResult()
        result.fail(f"o passo quebrou: {type(error).__name__}: {error}")
    elapsed = time.perf_counter() - started
    for line in result.lines:
        print(f"   {line}")
    for warning in result.warnings:
        print(f"   AVISO: {warning}")
    print(f"   -> {'passou' if result.ok else 'FALHOU'} em {elapsed:.1f} s", flush=True)
    return result, elapsed


def execute(names: list[str], ctx: Context, *, check_requirements: bool) -> list[Outcome]:
    """Run the steps in order, collecting what happened to each."""
    outcomes: list[Outcome] = []
    failed: set[str] = set()
    for name in names:
        blockers = [r for r in REQUIRES.get(name, ()) if r in failed] if check_requirements else []
        if blockers:
            print(f"\n== {name}: não rodou porque {', '.join(blockers)} falhou")
            outcomes.append(Outcome(name, "não rodou", 0.0))
            failed.add(name)
            continue
        result, elapsed = run_step(name, ctx)
        outcomes.append(Outcome(name, "passou" if result.ok else "FALHOU", elapsed))
        if not result.ok:
            failed.add(name)
    return outcomes


def render_report(outcomes: list[Outcome]) -> list[str]:
    """The closing report: one line per step with the time it took."""
    lines = ["| Passo | Resultado | Tempo |", "|---|---|---|"]
    lines.extend(f"| {o.name} | {o.status} | {o.seconds:.1f} s |" for o in outcomes)
    lines.append(f"| total | {'passou' if all(o.status == 'passou' for o in outcomes) else 'FALHOU'} "
                 f"| {sum(o.seconds for o in outcomes):.1f} s |")
    return lines


def parse_steps(requested: list[str]) -> list[str] | None:
    """The chosen steps in the order of STEP_ORDER, or None when one of them is unknown."""
    names = [n.strip() for item in requested for n in item.split(",") if n.strip()]
    unknown = [n for n in names if n not in STEP_ORDER]
    if unknown:
        print(f"passo desconhecido: {', '.join(unknown)}. Passos: {', '.join(STEP_ORDER)}")
        return None
    return [n for n in STEP_ORDER if n in names]


def main(argv: list[str] | None = None) -> int:
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description="Verifica o código do repositório.")
    parser.add_argument("--list", action="store_true", help="lista os passos, na ordem em que rodam")
    parser.add_argument("--step", action="append", default=[], help="passo a rodar (repetível, ou separado por vírgula)")
    parser.add_argument("--ci", action="store_true", help="modo CI: teste saltado é falha (também ligado pela variável CI)")
    parser.add_argument("--root", type=Path, default=DEFAULT_ROOT, help="raiz do repositório")
    parser.add_argument("--out-dir", type=Path, default=None, help=f"onde os passos deixam resultados (padrão: <raiz>/{OUTPUT_DIRECTORY})")
    args = parser.parse_args(argv)

    if args.list:
        for name in STEP_ORDER:
            print(name)
        return 0

    names = parse_steps(args.step) if args.step else list(STEP_ORDER)
    if names is None:
        return 2

    root = args.root.resolve()
    out_dir = (args.out_dir or root / OUTPUT_DIRECTORY).resolve()
    out_dir.mkdir(parents=True, exist_ok=True)
    ci = args.ci or bool(os.environ.get("CI"))
    ctx = Context(root=root, out_dir=out_dir, ci=ci)

    print(f"verify-code: {len(names)} passo(s), modo {'CI' if ci else 'local'}, saída em {out_dir.name}/")
    outcomes = execute(names, ctx, check_requirements=not args.step)
    report = render_report(outcomes)
    print("\n" + "\n".join(report))
    if ctx.summary_path is not None:
        with ctx.summary_path.open("a", encoding="utf-8", newline="\n") as summary:
            summary.write("### Passos de verificação do código\n\n" + "\n".join(report) + "\n\n")
    return 0 if all(o.status == "passou" for o in outcomes) else 1


if __name__ == "__main__":
    sys.exit(main())
