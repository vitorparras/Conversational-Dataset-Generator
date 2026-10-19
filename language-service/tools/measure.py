"""Measures a spaCy pipeline package: disk size, load time and resident memory (Linux only).

Run with `uv run --no-project`; see docs/operacao/015-medicao-do-modelo-de-linguagem.md. stdout
is one JSON document, stderr a table; messages to the operator are in Portuguese.
"""

from __future__ import annotations

import argparse
import importlib
import importlib.util
import json
import os
import platform
import resource
import statistics
import subprocess
import sys
import time
import zlib
from collections.abc import Callable, Mapping
from dataclasses import asdict, dataclass, fields, is_dataclass
from pathlib import Path
from types import MappingProxyType
from typing import cast

SENTENCES = (
    "PESSOA_1 falou com PESSOA_2 em CIDADE_A na segunda-feira e levou o carro para a oficina.",
    "Depois da reunião, PESSOA_3 mandou uma mensagem dizendo que chegaria a CIDADE_B mais tarde.",
    "Ontem PESSOA_2 contou que a empresa ORGANIZACAO_A fechou o contrato com PESSOA_4.",
)


class _MeasurementError(Exception):
    """An expected failure; its message is for the operator, in Portuguese."""


@dataclass(frozen=True)
class _Probe:
    import_seconds: float
    load_seconds: float
    analysis_seconds: float
    rss_start_kib: int
    rss_imported_kib: int
    rss_loaded_kib: int
    rss_analyzed_kib: int
    peak_rss_kib: int
    spacy_version: str
    pipes: tuple[str, ...]
    text_chars: int
    tokens: int


@dataclass(frozen=True)
class _Disk:
    bytes: int
    deflated_bytes: int


@dataclass(frozen=True)
class _Machine:
    os: str
    python: str
    cores: int
    memory_mib: int
    load_average_start: tuple[float, float, float]
    load_average_end: tuple[float, float, float]


@dataclass(frozen=True)
class _Report:
    model: str
    meta: Mapping[str, object]
    disk: _Disk
    machine: _Machine
    runs: int
    requested_chars: int
    probes: tuple[_Probe, ...]
    medians: Mapping[str, float]


def _current_rss_kib() -> int:
    for line in Path("/proc/self/status").read_text(encoding="ascii").splitlines():
        if line.startswith("VmRSS:"):
            return int(line.split()[1])
    raise _MeasurementError("VmRSS ausente em /proc/self/status")


def _synthetic_text(chars: int) -> str:
    """Placeholder-only Portuguese text of at least ``chars`` characters."""
    block = " ".join(SENTENCES) + " "
    return (block * (chars // len(block) + 1)).strip()


def _timed[T](call: Callable[[], T]) -> tuple[T, float]:
    started = time.perf_counter()
    result = call()
    return result, time.perf_counter() - started


def _probe(model: str, chars: int) -> _Probe:
    """Imports spaCy, loads ``model`` and analyses the synthetic text, noting time and RSS."""
    rss_start = _current_rss_kib()
    spacy, import_seconds = _timed(lambda: importlib.import_module("spacy"))
    rss_imported = _current_rss_kib()
    nlp, load_seconds = _timed(lambda: spacy.load(model))
    rss_loaded = _current_rss_kib()
    text = _synthetic_text(chars)
    doc, analysis_seconds = _timed(lambda: nlp(text))
    return _Probe(
        import_seconds=import_seconds,
        load_seconds=load_seconds,
        analysis_seconds=analysis_seconds,
        rss_start_kib=rss_start,
        rss_imported_kib=rss_imported,
        rss_loaded_kib=rss_loaded,
        rss_analyzed_kib=_current_rss_kib(),
        peak_rss_kib=resource.getrusage(resource.RUSAGE_SELF).ru_maxrss,
        spacy_version=str(spacy.__version__),
        pipes=tuple(str(name) for name in nlp.pipe_names),
        text_chars=len(text),
        tokens=len(doc),
    )


def _run_child(model: str, chars: int) -> _Probe:
    command = [sys.executable, str(Path(__file__).resolve()), "--probe", model, "--chars"]
    done = subprocess.run([*command, str(chars)], capture_output=True, text=True, check=False)
    if done.returncode != 0:
        last = (done.stderr.strip().splitlines() or ["sem saída de erro"])[-1]
        raise _MeasurementError(f"o processo de medição falhou: {last}")
    raw = json.loads(done.stdout)
    raw["pipes"] = tuple(raw["pipes"])
    return _Probe(**raw)


def _package_dir(model: str) -> Path:
    spec = importlib.util.find_spec(model)
    if spec is None or not spec.submodule_search_locations:
        raise _MeasurementError(f"o pacote {model} não está instalado neste ambiente")
    return Path(next(iter(spec.submodule_search_locations)))


def _deflated_size(path: Path) -> int:
    """Bytes of the file after raw deflate level 6, streamed in chunks."""
    compressor = zlib.compressobj(6, zlib.DEFLATED, -15)
    total = 0
    with path.open("rb") as stream:
        while chunk := stream.read(1 << 20):
            total += len(compressor.compress(chunk))
    return total + len(compressor.flush())


def _measure_disk(root: Path) -> _Disk:
    files = tuple(
        path
        for path in root.rglob("*")
        if path.is_file() and "__pycache__" not in path.relative_to(root).parts
    )
    return _Disk(
        bytes=sum(path.stat().st_size for path in files),
        deflated_bytes=sum(_deflated_size(path) for path in files),
    )


def _read_meta(root: Path) -> Mapping[str, object]:
    found = sorted(root.glob("*/meta.json"))
    if not found:
        raise _MeasurementError("meta.json não encontrado dentro do pacote")
    meta: dict[str, object] = json.loads(found[0].read_text(encoding="utf-8"))
    keys = ("name", "version", "license", "spacy_version", "pipeline", "vectors", "size")
    return MappingProxyType({key: meta[key] for key in keys if key in meta})


def _machine_now(start: tuple[float, float, float]) -> _Machine:
    release = platform.freedesktop_os_release()
    return _Machine(
        os=f"{release.get('PRETTY_NAME', platform.system())}, {platform.machine()}",
        python=platform.python_version(),
        cores=os.cpu_count() or 0,
        memory_mib=int(Path("/proc/meminfo").read_text().split()[1]) // 1024,
        load_average_start=start,
        load_average_end=os.getloadavg(),
    )


def _medians(probes: tuple[_Probe, ...]) -> Mapping[str, float]:
    numeric = [n for n, value in asdict(probes[0]).items() if isinstance(value, int | float)]
    return MappingProxyType({n: statistics.median(getattr(p, n) for p in probes) for n in numeric})


def _build_report(model: str, runs: int, chars: int) -> _Report:
    start = os.getloadavg()
    root = _package_dir(model)
    disk = _measure_disk(root)
    probes = tuple(_run_child(model, chars) for _ in range(runs))
    return _Report(
        model, _read_meta(root), disk, _machine_now(start), runs, chars, probes, _medians(probes)
    )


def _table(report: _Report) -> str:
    loads = [p.load_seconds for p in report.probes]
    rows = {
        "disk_bytes": str(report.disk.bytes),
        "disk_deflated_bytes": str(report.disk.deflated_bytes),
        **{name: f"{value:g}" for name, value in report.medians.items()},
        "load_seconds_range": f"{min(loads):.2f}-{max(loads):.2f}",
    }
    header = (
        f"{report.model}: mediana de {report.runs} execuções, "
        f"{report.probes[0].text_chars} caracteres (pedidos {report.requested_chars})"
    )
    return "\n".join([header, *(f"{name:<24}{value:>16}" for name, value in rows.items())])


def _encode(value: object) -> object:
    if is_dataclass(value) and not isinstance(value, type):
        return {field.name: getattr(value, field.name) for field in fields(value)}
    if isinstance(value, MappingProxyType):
        return dict(cast("MappingProxyType[str, object]", value))
    raise TypeError(f"not serialisable: {type(value).__name__}")


def _parse(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Mede um pacote de modelo spaCy instalado.")
    parser.add_argument("model", help="nome do pacote, por exemplo pt_core_news_md")
    parser.add_argument("--runs", type=int, default=7, help="processos que carregam")
    parser.add_argument("--chars", type=int, default=20_000, help="tamanho do texto")
    parser.add_argument("--probe", action="store_true", help=argparse.SUPPRESS)
    return parser.parse_args(argv)


def main(argv: list[str]) -> int:
    options = _parse(argv)
    try:
        if options.probe:
            print(json.dumps(asdict(_probe(options.model, options.chars))))
            return 0
        report = _build_report(options.model, options.runs, options.chars)
    except _MeasurementError as error:
        print(f"measure.py: {error}", file=sys.stderr)
        return 1
    print(json.dumps(report, default=_encode, indent=2))
    print(_table(report), file=sys.stderr)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
