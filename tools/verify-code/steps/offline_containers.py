"""The containers behind the offline_start step: what each one is for and how it comes and goes.

Three images are built with a network. The build container assembles the service folder with one,
into a volume. The database container has no network at all, and the host container joins its
network namespace, so both see the loopback and nothing else: the host reaches the server and the
language service it starts at 127.0.0.1, as it would with a database on the same machine.
"""

from __future__ import annotations

import secrets
from dataclasses import dataclass, field
from pathlib import Path

from steps.common import CommandOutcome, Context, StepResult, failed, tail

NAME = "offline_start"
FOLDER = "tools/verify-code/offline_start"
DATABASE_FOLDER = "tools/database"
MODEL = "pt_core_news_md"
SYSTEM = "linux-x64"
BUILD = "cdg-offline-build-run"
DATABASE = "cdg-offline-database-run"
HOST = "cdg-offline-host-run"
VOLUME = "cdg-offline-service"
CONTAINERS = (HOST, DATABASE, BUILD)
READY_SECONDS = 120
ASSEMBLE = (
    "set -e; cp -r /input /home/build/repo; "
    "pip install -q --user -r /home/build/repo/tools/verify-code/service-requirements.txt; "
    f"python /home/build/repo/tools/build-service/service_folder.py --system {SYSTEM} "
    f"--model {MODEL} --output /out"
)
WAIT_FOR_DATABASE = (
    f"i=0; until pg_isready -h 127.0.0.1 -q; do i=$((i+1)); [ $i -gt {READY_SECONDS} ] && exit 1; "
    "sleep 1; done"
)


@dataclass(frozen=True)
class Image:
    """A tag, the folder it is built from and, if not the folder's own Dockerfile, the file."""

    tag: str
    folder: str
    dockerfile: str | None = None

    def command(self) -> tuple[str, ...]:
        file = ("--file", f"{self.folder}/{self.dockerfile}") if self.dockerfile else ()
        return ("build", "--quiet", *file, "--tag", self.tag, self.folder)


BUILDER = Image("cdg-offline-build:verify", FOLDER, "build.Dockerfile")
RUNNER = Image("cdg-offline-start:verify", FOLDER, "start.Dockerfile")
SERVER = Image("cdg-offline-database:verify", DATABASE_FOLDER)


@dataclass(frozen=True)
class Credentials:
    """The passwords of the throwaway server: made for this run, they live in no file."""

    superuser: str = field(default_factory=lambda: secrets.token_hex(16))
    owner: str = field(default_factory=lambda: secrets.token_hex(16))
    application: str = field(default_factory=lambda: secrets.token_hex(16))


def docker(ctx: Context, *arguments: str) -> CommandOutcome:
    return ctx.run(["docker", *arguments], ctx.root)


def build_images(ctx: Context) -> StepResult | None:
    """None when the three images are built; otherwise the failure, with docker's output."""
    for image in (BUILDER, RUNNER, SERVER):
        built = docker(ctx, *image.command())
        if not built.ok:
            return failed(NAME, f"a imagem {image.tag} não foi construída", *tail(built.text))
    return None


def remove_all(ctx: Context) -> None:
    """Removes the three containers and the volume, whatever state an earlier run left them in."""
    _ = docker(ctx, "rm", "--force", *CONTAINERS)
    _ = docker(ctx, "volume", "rm", "--force", VOLUME)


def assemble_service(ctx: Context, clone: Path) -> StepResult | None:
    """Assembles the service folder of ``clone`` inside the build container, into the volume."""
    steps = (
        ("volume", "create", VOLUME),
        (
            "create",
            "--name",
            BUILD,
            "--volume",
            f"{VOLUME}:/out",
            BUILDER.tag,
            "sh",
            "-c",
            ASSEMBLE,
        ),
        ("cp", f"{clone}/.", f"{BUILD}:/input"),
        ("start", "--attach", BUILD),
    )
    for arguments in steps:
        outcome = docker(ctx, *arguments)
        if not outcome.ok:
            return failed(NAME, "a pasta do serviço não foi montada", *tail(outcome.text))
    return None


def start_database(ctx: Context, credentials: Credentials) -> StepResult | None:
    """Runs the server with no network and waits until it answers on its own loopback."""
    environment = (
        f"POSTGRES_PASSWORD={credentials.superuser}",
        f"DATABASE_OWNER_PASSWORD={credentials.owner}",
        f"DATABASE_APP_PASSWORD={credentials.application}",
    )
    options = [item for variable in environment for item in ("--env", variable)]
    run = docker(
        ctx, "run", "--detach", "--name", DATABASE, "--network", "none", *options, SERVER.tag
    )
    waited = docker(ctx, "exec", DATABASE, "sh", "-c", WAIT_FOR_DATABASE)
    if not run.ok or not waited.ok:
        return failed(NAME, "o servidor do banco não ficou de pé", *tail(run.text + waited.text))
    return None


def run_host(ctx: Context, published: Path, credentials: Credentials) -> CommandOutcome:
    """Runs the host container in the network of the server, with the service folder beside it."""
    folder = f"type=volume,src={VOLUME},dst=/work/language-service,volume-subpath=language-service"
    environment = (f"OWNER_PASSWORD={credentials.owner}", f"APP_PASSWORD={credentials.application}")
    options = [item for variable in environment for item in ("--env", variable)]
    create = ["create", "--name", HOST, "--network", f"container:{DATABASE}", *options]
    created = docker(ctx, *create, "--mount", f"{folder},readonly", RUNNER.tag)
    copied = created if not created.ok else docker(ctx, "cp", f"{published}/.", f"{HOST}:/app")
    return copied if not copied.ok else docker(ctx, "start", "--attach", HOST)


def collect(ctx: Context, results: Path) -> None:
    """Copies what the host container wrote under /trace into ``results``."""
    _ = docker(ctx, "cp", f"{HOST}:/trace/.", str(results))
