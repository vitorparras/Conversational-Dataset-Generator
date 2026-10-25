"""Publishes the core twice, each from its own clean copy of HEAD, and compares them byte by byte.

The copies are clones in a temporary folder, never the working tree, so a change not committed
cannot reach the artifact. ContinuousIntegrationBuild maps the source paths, which is what lets two
copies in different folders publish the same bytes; the restore is locked, so nothing is resolved.
"""

from __future__ import annotations

import hashlib
import tempfile
from collections.abc import Mapping
from dataclasses import dataclass
from pathlib import Path

from steps.common import Context, StepResult, failed, passed, tail

NAME = "package"
PROJECT = "core/src/Core.Host/Core.Host.csproj"
HOST_ASSEMBLY = "Core.Host.dll"
CLEAN_TREE = ("BuildInfo.WorkingTree", "Clean")
NOT_CLEAN = "a cópia não está limpa: o artefato não sairia só do commit"
LOCK_FAILED = "restauração travada falhou: lock divergente ou pacote ausente do cache e da fonte"
PUBLISH = (
    "--no-restore",
    "--configuration",
    "Release",
    "-p:ContinuousIntegrationBuild=true",
    "-nodeReuse:false",
    "-p:UseSharedCompilation=false",
)


@dataclass(frozen=True)
class Revision:
    """The commit both copies are built from, and the remote SourceLink reads in each copy."""

    commit: str
    origin: str | None


def stamp(key: str, value: str) -> bytes:
    """An assembly metadata pair as the compiler serializes it: each string after its length."""
    return b"".join(bytes([len(text)]) + text.encode("ascii") for text in (key, value))


def checked(ctx: Context, command: list[str], cwd: Path, problem: str) -> StepResult | None:
    """None when the command succeeds; otherwise the failure, with its output."""
    outcome = ctx.run(command, cwd)
    return None if outcome.ok else failed(NAME, problem, *tail(outcome.text))


def clone(ctx: Context, revision: Revision, copy: Path) -> StepResult | None:
    """A clone of the commit at ``copy``, with the working tree's remote, that git sees as clean."""
    commands = [
        ["git", "clone", "--quiet", "--no-checkout", "--no-hardlinks", str(ctx.root), str(copy)],
        ["git", "-C", str(copy), "checkout", "--quiet", "--detach", revision.commit],
    ]
    if revision.origin:
        commands.append(["git", "-C", str(copy), "remote", "set-url", "origin", revision.origin])
    for command in commands:
        problem = checked(ctx, command, ctx.root, "a cópia limpa do repositório falhou")
        if problem is not None:
            return problem
    status = ctx.run(["git", "-C", str(copy), "status", "--porcelain"], ctx.root)
    if not status.ok or status.text.strip():
        return failed(NAME, NOT_CLEAN, *tail(status.text))
    return None


def publish(ctx: Context, copy: Path, output: Path) -> StepResult | None:
    """Restores in locked mode and publishes the host of ``copy`` into ``output``."""
    restore = ["dotnet", "restore", PROJECT, "--locked-mode"]
    command = ["dotnet", "publish", PROJECT, *PUBLISH, "--output", str(output)]
    problem = checked(ctx, restore, copy, LOCK_FAILED)
    return problem or checked(ctx, command, copy, "a publicação do núcleo falhou na cópia limpa")


def unstamped(output: Path, commit: str) -> StepResult | None:
    """None when the host says it came from ``commit`` on a clean tree; otherwise what it lacks."""
    assembly = output / HOST_ASSEMBLY
    image = assembly.read_bytes() if assembly.is_file() else b""
    expected = (("BuildInfo.Commit", commit), CLEAN_TREE)
    missing = [f"{key}={value}" for key, value in expected if stamp(key, value) not in image]
    if missing:
        absent = ", ".join(missing)
        return failed(NAME, f"{HOST_ASSEMBLY} não declara {absent}: não se diz de cópia limpa")
    return None


def digests(folder: Path) -> dict[str, str]:
    """The SHA-256 of every file under ``folder``, by its path relative to it."""
    return {
        path.relative_to(folder).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest()
        for path in sorted(folder.rglob("*"))
        if path.is_file()
    }


def build(ctx: Context, revision: Revision, copy: Path) -> dict[str, str] | StepResult:
    """The digests of what one clean copy publishes, or why it published nothing comparable."""
    output = copy.with_name(f"{copy.name}-publish")
    problem = clone(ctx, revision, copy) or publish(ctx, copy, output)
    problem = problem or unstamped(output, revision.commit)
    return problem or digests(output)


def difference(first: str | None, second: str | None) -> str:
    if first is None:
        return "só na segunda construção"
    if second is None:
        return "só na primeira construção"
    return "conteúdo difere entre as duas construções"


def compare(first: Mapping[str, str], second: Mapping[str, str]) -> StepResult:
    """Passes when both builds published the same bytes in the same files; names each mismatch."""
    names = sorted(first.keys() | second.keys())
    differing = [
        f"{name}: {difference(first.get(name), second.get(name))}"
        for name in names
        if first.get(name) != second.get(name)
    ]
    if differing:
        summary = f"as duas construções diferem em {len(differing)} de {len(names)} arquivos"
        return failed(NAME, summary, *differing)
    return passed(
        NAME,
        f"{len(names)} arquivos publicados iguais byte a byte por duas cópias limpas",
        f"{HOST_ASSEMBLY} declara o commit e a árvore limpa",
    )


def run(ctx: Context) -> StepResult:
    if not (ctx.root / PROJECT).is_file():
        return failed(NAME, f"{PROJECT} não existe: não há o que publicar")
    head = ctx.run(["git", "rev-parse", "HEAD"], ctx.root)
    if not head.ok:
        return failed(NAME, "o commit de HEAD não foi lido", *tail(head.text))
    origin = ctx.run(["git", "remote", "get-url", "origin"], ctx.root)
    revision = Revision(head.text.strip(), origin.text.strip() if origin.ok else None)
    with tempfile.TemporaryDirectory(prefix="cdg-package-", ignore_cleanup_errors=True) as scratch:
        first = build(ctx, revision, Path(scratch) / "first")
        if isinstance(first, StepResult):
            return first
        second = build(ctx, revision, Path(scratch) / "second")
        if isinstance(second, StepResult):
            return second
    return compare(first, second)
