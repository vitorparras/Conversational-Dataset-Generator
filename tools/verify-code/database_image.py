"""The name of the test database image: the registry path of the repository plus a tag that is the
hash of the files that define the image.

    python tools/verify-code/database_image.py owner/repository
    python tools/verify-code/database_image.py --provide owner/repository

The image is built once for a content and published under that name, so a job pulls it instead of
building it. Any change to a file of tools/database gives another tag; nothing else does. Only a
run of main publishes (PUBLISH=true); a pull request pulls what main published and builds locally
what it did not, and the registry login is dropped before the job goes on to run code of the PR.
"""

from __future__ import annotations

import hashlib
import os
import re
import subprocess
import sys
from collections.abc import Callable
from dataclasses import dataclass
from pathlib import Path

REGISTRY = "ghcr.io"
IMAGE = "cdg-test-database"
DEFINITION = Path("tools") / "database"
TAG_LENGTH = 16
USAGE = 2
RETRY = Path(__file__).with_name("retry.py")
MISSING = ("manifest unknown", "not found", "name unknown")
APT_ERROR = re.compile(r"^(#\d+ +[\d.]+ +)?E: ")
NO_APT_LINE = "o apt não escreveu nenhuma linha `E:`; a causa está no fim do log do build"
Runner = Callable[[list[str], str | None], tuple[int, str]]


def content_hash(root: Path) -> str:
    """The first hex digits of the hash of the definition files, by relative path and bytes."""
    digest = hashlib.sha256()
    folder = root / DEFINITION
    for path in sorted(p for p in folder.rglob("*") if p.is_file()):
        digest.update(path.relative_to(folder).as_posix().encode("utf-8") + b"\0")
        digest.update(hashlib.sha256(path.read_bytes().replace(b"\r\n", b"\n")).digest())
    return digest.hexdigest()[:TAG_LENGTH]


def image_name(root: Path, repository: str) -> str:
    """The registry name of the image of this content; the registry wants it in lower case."""
    return f"{REGISTRY}/{repository.lower()}/{IMAGE}:{content_hash(root)}"


def run(args: list[str], stdin: str | None = None) -> tuple[int, str]:
    """Runs a command, printing what it writes as it writes it; returns its code and its output."""
    process = subprocess.Popen(
        args,
        stdin=subprocess.PIPE if stdin is not None else subprocess.DEVNULL,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
    )
    if stdin is not None and process.stdin is not None:
        process.stdin.write(stdin)
        process.stdin.close()
    lines: list[str] = []
    assert process.stdout is not None
    for line in process.stdout:
        print(line, end="", flush=True)
        lines.append(line)
    return process.wait(), "".join(lines)


@dataclass(frozen=True)
class Provision:
    """What a job asks of the image: where it lives, who reads it, whether this run may write it."""

    image: str
    token: str
    actor: str
    publish: bool
    source: str
    env_file: Path
    summary: Path


def provide(job: Provision, runner: Runner = run) -> int:
    """Pulls the image of the content, or builds it (and publishes it, if allowed), and names it in
    the environment of the next steps. The login never outlives this call."""
    login = ["docker", "login", REGISTRY, "--username", job.actor, "--password-stdin"]
    logged = bool(job.token) and runner(login, job.token)[0] == 0
    try:
        if logged and pulled(job, runner):
            return 0
        return built(job, runner, logged)
    finally:
        if logged:
            runner(["docker", "logout", REGISTRY], None)


def pulled(job: Provision, runner: Runner) -> bool:
    code, output = runner(["docker", "pull", "--quiet", job.image], None)
    if code != 0:
        if not any(text in output.lower() for text in MISSING):
            tail = output.strip()[-200:]
            print(f"::notice::the pull of the database image failed, it is built here: {tail}")
        return False
    print(f"Imagem do banco baixada do GHCR: {job.image}")
    export(job)
    return True


def built(job: Provision, runner: Runner, logged: bool) -> int:
    build = [sys.executable, str(RETRY), "--", "docker", "build", "--progress=plain"]
    build += [
        "--label",
        f"org.opencontainers.image.source={job.source}",
        "--tag",
        job.image,
        str(DEFINITION),
    ]
    code, output = runner(build, None)
    if code != 0:
        explain(job, output)
        return 1
    if job.publish and logged and runner(["docker", "push", "--quiet", job.image], None)[0] != 0:
        print(
            "::warning::a imagem do banco não foi publicada no GHCR (sem permissão de pacote?); "
            "este job usa a construída aqui"
        )
    export(job)
    return 0


def explain(job: Provision, output: str) -> None:
    """The lines `E:` of the apt, once each, in the summary of the job."""
    seen = dict.fromkeys(
        APT_ERROR.sub("", line) for line in output.splitlines() if APT_ERROR.match(line)
    )
    detail = "\n".join(seen) or NO_APT_LINE
    with job.summary.open("a", encoding="utf-8") as summary:
        summary.write(f"### A imagem do banco de teste não foi construída\n\n```\n{detail}\n```\n")


def export(job: Provision) -> None:
    with job.env_file.open("a", encoding="utf-8") as env:
        env.write(f"TEST_DATABASE_IMAGE={job.image}\n")


def provision_from(environment: dict[str, str], repository: str) -> Provision:
    root = Path(__file__).resolve().parents[2]
    return Provision(
        image=image_name(root, repository),
        token=environment.get("TOKEN", ""),
        actor=environment.get("GITHUB_ACTOR", ""),
        publish=environment.get("PUBLISH") == "true",
        source=f"https://github.com/{repository}",
        env_file=Path(environment["GITHUB_ENV"]),
        summary=Path(environment["GITHUB_STEP_SUMMARY"]),
    )


def main(arguments: list[str]) -> int:
    providing = arguments[:1] == ["--provide"]
    names = arguments[1:] if providing else arguments
    if len(names) != 1 or "/" not in names[0]:
        print("uso: database_image.py [--provide] <dono>/<repositório>", file=sys.stderr)
        return USAGE
    if providing:
        return provide(provision_from(dict(os.environ), names[0]))
    print(image_name(Path(__file__).resolve().parents[2], names[0]))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
