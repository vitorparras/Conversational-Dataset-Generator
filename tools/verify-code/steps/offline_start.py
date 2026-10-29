"""Starts the published core in a container with no network and fails on any attempt to reach one.

The container is created with `--network none`, so the loopback is the only interface it has, and
the host runs under strace inside it (tools/verify-code/offline_start/start.sh). The host is the
single executable `package` publishes, told to listen on the loopback with the fake recognizer: the
published folder carries no language service, and no database is declared. The step passes only if
the host printed the line that says it serves and left with 0 when stopped, and the trace shows no
connection outside the loopback and no DNS query (see steps/strace_log.py).
"""

from __future__ import annotations

import re
import tempfile
from dataclasses import replace
from pathlib import Path

from steps import package, strace_log
from steps.common import CommandOutcome, Context, StepResult, failed, passed, reset_directory, tail

NAME = "offline_start"
TARGET = "linux-x64"
IMAGE_FOLDER = "tools/verify-code/offline_start"
IMAGE = "cdg-offline-start:verify"
CONTAINER = "cdg-offline-start-run"
RESULTS = "offline-start"
READY = re.compile(r"Now listening on: http://127\.0\.0\.1:\d+")
LOOPBACK_ONLY = ["lo"]


def docker(ctx: Context, *arguments: str) -> CommandOutcome:
    return ctx.run(["docker", *arguments], ctx.root)


def start(ctx: Context, published: Path, results: Path) -> CommandOutcome:
    """Runs the container with the published folder in it and copies its /trace into ``results``.

    The container is removed on the way out, also when a command fails or the run is interrupted.
    """
    _ = docker(ctx, "rm", "--force", CONTAINER)
    try:
        create = docker(ctx, "create", "--name", CONTAINER, "--network", "none", IMAGE)
        copy = create if not create.ok else docker(ctx, "cp", f"{published}/.", f"{CONTAINER}:/app")
        if not copy.ok:
            return copy
        started = docker(ctx, "start", "--attach", CONTAINER)
        _ = docker(ctx, "cp", f"{CONTAINER}:/trace/.", str(results))
        return started
    finally:
        _ = docker(ctx, "rm", "--force", CONTAINER)


def read(results: Path, name: str) -> str:
    """What the container wrote under ``name``; empty when it never got to write it."""
    path = results / name
    return path.read_text(encoding="utf-8", errors="replace") if path.is_file() else ""


def problems_of(started: CommandOutcome, results: Path) -> tuple[str, ...]:
    """Everything wrong with the start, from the container's outcome and the files it left."""
    interfaces, host, trace = (
        read(results, name) for name in ("interfaces.txt", "host.log", "strace.txt")
    )
    found: list[str] = []
    if interfaces.split() != LOOPBACK_ONLY:
        found.append(
            f"o contêiner não ficou só com o loopback; interfaces lidas: {interfaces.split()}"
        )
    if not trace.strip():
        found.append("o rastreador não registrou nada: sem rastro não há prova")
    if READY.search(host) is None:
        found.append("a partida não chegou à linha de pronto (`Now listening on`)")
    if started.code != 0:
        found.append(f"o núcleo saiu com código {started.code}, esperado 0")
    return (*found, *strace_log.refusals(trace))


def judge(started: CommandOutcome, results: Path) -> StepResult:
    """Passes only for a start that served, stopped with 0 and tried nothing past the loopback."""
    found = problems_of(started, results)
    host = read(results, "host.log")
    if found:
        return failed(NAME, *found, *tail(host or started.text))
    seen = strace_log.seen(read(results, "strace.txt"))
    return passed(
        NAME,
        "o núcleo publicado subiu num contêiner só com o loopback, serviu e saiu com 0",
        f"chamadas com destino vistas pelo rastreador: {', '.join(seen) or 'nenhuma'}",
        "nenhuma conexão fora do loopback e nenhuma consulta de DNS",
    )


def run(ctx: Context) -> StepResult:
    if not (ctx.root / package.PROJECT).is_file():
        return failed(NAME, f"{package.PROJECT} não existe: não há o que subir")
    image = docker(ctx, "build", "--quiet", "--tag", IMAGE, IMAGE_FOLDER)
    if not image.ok:
        return failed(NAME, "a imagem com o rastreador não foi construída", *tail(image.text))
    results = ctx.out_dir / RESULTS
    reset_directory(results)
    with tempfile.TemporaryDirectory(prefix="cdg-offline-", ignore_cleanup_errors=True) as scratch:
        published = Path(scratch)
        problem = package.publish(ctx, ctx.root, published, TARGET)
        if problem is not None:
            return replace(problem, name=NAME)
        return judge(start(ctx, published, results), results)
