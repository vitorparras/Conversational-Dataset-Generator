"""Starts the published core as an operator would, with no network, and fails on any attempt.

The host runs in a container that shares the network namespace of the database container, which
has no network at all: the loopback is the only interface. The service folder comes from
tools/build-service, assembled beforehand in a container that does have a network (see
steps/offline_containers.py). Inside, under strace (tools/verify-code/offline_start/start.sh), the
host migrates the schema, then starts with the real recognizer: it checks the database, the
service folder and the language service it starts, and serves. The step passes only if the host
reached the line that says it serves with the schema, the contract and the service all present,
both runs left with 0, and the traces show no connection outside the loopback and no DNS query
(see steps/strace_log.py).
"""

from __future__ import annotations

import re
import tempfile
from dataclasses import dataclass, replace
from pathlib import Path

from steps import offline_containers as containers
from steps import package, strace_log
from steps.common import Context, StepResult, failed, passed, reset_directory, tail

NAME = containers.NAME
RESULTS = "offline-start"
READY = re.compile(r"Now listening on: http://127\.0\.0\.1:\d+")
VERSIONS = re.compile(r"Versões em execução .*")
MUST_BE_PRESENT = ("SchemaVersion", "ContractVersion", "LanguageServiceVersion")
LOOPBACK_ONLY = ["lo"]
PHASES = ("migrate", "serve")


@dataclass(frozen=True)
class Evidence:
    """What the host container left under /trace, read from the folder it was copied into."""

    results: Path

    def text(self, name: str) -> str:
        """The file ``name``; empty when the container never got to write it."""
        path = self.results / name
        return path.read_text(encoding="utf-8", errors="replace") if path.is_file() else ""

    def codes(self) -> dict[str, str]:
        """The `key=value` lines of result.txt: the exit codes and whether the host served."""
        pairs = (line.split("=", 1) for line in self.text("result.txt").splitlines() if "=" in line)
        return {key: value for key, value in pairs}

    def missing_versions(self) -> list[str]:
        """The versions the startup line should carry and shows absent; all of them if no line."""
        line = VERSIONS.search(self.text("serve.log"))
        shown = line.group(0) if line else ""
        return [name for name in MUST_BE_PRESENT if f"{name}=<absent>" in shown or not shown]


def problems_of(evidence: Evidence) -> tuple[str, ...]:
    """Everything wrong with the start, from the files the container left."""
    codes = evidence.codes()
    found: list[str] = []
    if evidence.text("interfaces.txt").split() != LOOPBACK_ONLY:
        found.append(
            f"o namespace não ficou só com o loopback: {evidence.text('interfaces.txt')!r}"
        )
    for phase in PHASES:
        if not evidence.text(f"{phase}.strace").strip():
            found.append(f"o rastreador não registrou nada em {phase}: sem rastro não há prova")
        if codes.get(phase) != "0":
            found.append(f"{phase} saiu com código {codes.get(phase, 'nenhum')}, esperado 0")
    if codes.get("ready") != "yes" or READY.search(evidence.text("serve.log")) is None:
        found.append("a partida não chegou à linha de pronto (`Now listening on`)")
    found += [f"{name} ausente na linha de versões" for name in evidence.missing_versions()]
    return tuple(found)


def refusals_of(evidence: Evidence) -> tuple[str, ...]:
    """What the traces of both runs show that the start must not do, each tagged with its run."""
    return tuple(
        f"{phase}: {refusal}"
        for phase in PHASES
        for refusal in strace_log.refusals(evidence.text(f"{phase}.strace"))
    )


def judge(evidence: Evidence) -> StepResult:
    """Passes only for a start that served with all present and tried nothing past the loopback."""
    found = (*problems_of(evidence), *refusals_of(evidence))
    if found:
        logs = evidence.text("serve.log") or evidence.text("migrate.log")
        return failed(NAME, *found, *tail(logs))
    seen = strace_log.seen(evidence.text("migrate.strace") + evidence.text("serve.strace"))
    return passed(
        NAME,
        "migração e partida do núcleo publicado sem rota de rede: banco, pasta e serviço de pé",
        f"chamadas com destino vistas pelo rastreador: {', '.join(seen) or 'nenhuma'}",
        "nenhuma conexão fora do loopback e nenhuma consulta de DNS, nos dois processos",
    )


def prepare(ctx: Context, scratch: Path) -> StepResult | None:
    """Publishes the host into ``scratch`` and clones HEAD there; None when both worked."""
    head = ctx.run(["git", "rev-parse", "HEAD"], ctx.root)
    if not head.ok:
        return failed(NAME, "o commit de HEAD não foi lido", *tail(head.text))
    published = scratch / "published"
    published.mkdir()
    revision = package.Revision(head.text.strip(), None)
    problem = package.publish(ctx, ctx.root, published, containers.SYSTEM)
    problem = problem or package.clone(ctx, revision, scratch / "clone")
    return replace(problem, name=NAME) if problem else None


def serve(ctx: Context, scratch: Path) -> StepResult:
    """Assembles the service folder of the clone, starts the server and the host, and judges."""
    problem = prepare(ctx, scratch)
    credentials = containers.Credentials()
    problem = problem or containers.assemble_service(ctx, scratch / "clone")
    problem = problem or containers.start_database(ctx, credentials)
    if problem:
        return problem
    containers.run_host(ctx, scratch / "published", credentials)
    results = ctx.out_dir / RESULTS
    reset_directory(results)
    containers.collect(ctx, results)
    return judge(Evidence(results))


def run(ctx: Context) -> StepResult:
    if not (ctx.root / package.PROJECT).is_file():
        return failed(NAME, f"{package.PROJECT} não existe: não há o que subir")
    problem = containers.build_images(ctx)
    if problem is not None:
        return problem
    containers.remove_all(ctx)
    try:
        with tempfile.TemporaryDirectory(prefix="cdg-offline-", ignore_cleanup_errors=True) as tmp:
            return serve(ctx, Path(tmp))
    finally:
        containers.remove_all(ctx)
