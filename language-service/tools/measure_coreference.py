"""Measures a coreference model over the synthetic conversations of the gold corpus.

Run with `uv run --no-project`; see docs/operacao/015-medicao-do-modelo-de-linguagem.md. stdout
is one JSON document, stderr a table and the verdict; messages to the operator are in Portuguese.
"""

from __future__ import annotations

import argparse
import json
import os
import platform
import sys
import time
from collections.abc import Callable, Mapping
from dataclasses import asdict, fields
from pathlib import Path

from coreference import resolvers
from coreference.corpus import load
from coreference.measurement import Probe, Report, Resolver, decimal, measure, shortfalls
from coreference.sizing import process_memory

CORPUS = Path(__file__).resolve().parent / "coreference" / "fixtures" / "conversas.json"
MIN_PRECISION = 0.95
SIZES = (10, 40, 160, 640)
MIB = 1 << 20
RESOLVERS: Mapping[str, Callable[[str], Resolver]] = {"stanza": resolvers.stanza}
PROBE = Probe(clock=time.perf_counter, memory=process_memory)


def _parse(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Mede um modelo de correferência no corpus ouro.")
    parser.add_argument("resolver", choices=sorted(RESOLVERS), help="como carregar o modelo")
    parser.add_argument("model", help="o que o resolvedor carrega, como o caminho do modelo")
    parser.add_argument("--corpus", type=Path, default=CORPUS, help="o corpus ouro")
    parser.add_argument("--min-precision", type=float, default=MIN_PRECISION, help="o limiar")
    parser.add_argument("--sizes", type=int, nargs="+", default=SIZES, help="mensagens por medida")
    return parser.parse_args(argv)


def _table(report: Report, load_seconds: float) -> str:
    ratios = {field.name: getattr(report.scores, field.name) for field in fields(report.scores)}
    rows = [
        f"{name:<18}{ratio.hits:>6}/{ratio.total:<6}{decimal(ratio.value):>8}"
        for name, ratio in ratios.items()
    ]
    sizes = [
        f"{row.messages:>5} mensagens {row.chars:>8} caracteres {row.seconds:8.2f} s "
        f"RSS {row.memory.rss / MIB:8.1f} MiB pico {row.memory.peak / MIB:8.1f} MiB"
        for row in report.sizes
    ]
    loaded = f"carga {load_seconds:.2f} s, RSS carregado {report.loaded.rss / MIB:.1f} MiB"
    return "\n".join([*rows, loaded, *sizes])


def _machine() -> dict[str, object]:
    return {"os": platform.platform(), "python": platform.python_version(), "cores": os.cpu_count()}


def main(argv: list[str]) -> int:
    options = _parse(argv)
    started = PROBE.clock()
    resolver = RESOLVERS[options.resolver](options.model)
    load_seconds = PROBE.clock() - started
    report = measure(load(options.corpus), resolver, options.sizes, PROBE)
    document = {"model": options.model, "machine": _machine(), "load_seconds": load_seconds}
    print(json.dumps({**document, **asdict(report)}, indent=2))
    print(_table(report, load_seconds), file=sys.stderr)
    refusals = shortfalls(report.scores, options.min_precision)
    for refusal in refusals:
        print(f"measure_coreference.py: reprovado: {refusal}", file=sys.stderr)
    return 1 if refusals else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
