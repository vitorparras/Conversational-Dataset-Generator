"""Publishes the core twice per target, each time from its own clean copy of HEAD, and compares.

The copies are clones in a temporary folder, never the working tree, so a change not committed
cannot reach the artifact. ContinuousIntegrationBuild maps the source paths, which is what lets two
copies in different folders publish the same bytes; the restore is locked, so nothing is resolved.
Each target publishes one executable beside its settings file, and nothing else: a library left
beside it is a dependency the destination would have to provide.
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
TARGETS = ("linux-x64", "win-x64")
SETTINGS_FILE = "appsettings.json"
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


def executable(target: str) -> str:
    """The name of the one executable a publish for ``target`` produces."""
    return "Core.Host.exe" if target.startswith("win-") else "Core.Host"


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


def publish(ctx: Context, copy: Path, output: Path, target: str) -> StepResult | None:
    """Restores in locked mode for ``target`` and publishes the host of ``copy`` into ``output``."""
    restore = ["dotnet", "restore", PROJECT, "--locked-mode", f"-p:RuntimeIdentifier={target}"]
    command = ["dotnet", "publish", PROJECT, *PUBLISH, "--runtime", target, "--output", str(output)]
    problem = checked(ctx, restore, copy, LOCK_FAILED)
    problem = problem or checked(ctx, command, copy, f"a publicação do núcleo para {target} falhou")
    return problem or single(output, target)


def single(output: Path, target: str) -> StepResult | None:
    """None when ``output`` holds the executable and the settings file, and nothing else."""
    expected = {executable(target), SETTINGS_FILE}
    found = {path.name for path in output.iterdir()} if output.is_dir() else set[str]()
    extra, missing = sorted(found - expected), sorted(expected - found)
    if extra or missing:
        listed = [f"sobra {name}" for name in extra] + [f"falta {name}" for name in missing]
        return failed(NAME, f"{target}: a publicação não é um executável único", *listed)
    return None


def unstamped(output: Path, commit: str, target: str) -> StepResult | None:
    """None when the executable says it came from ``commit`` on a clean tree for ``target``."""
    image = (output / executable(target)).read_bytes()
    expected = (("BuildInfo.Commit", commit), CLEAN_TREE, ("BuildInfo.RuntimeIdentifier", target))
    missing = [f"{key}={value}" for key, value in expected if stamp(key, value) not in image]
    if missing:
        absent = ", ".join(missing)
        return failed(NAME, f"{target}: {executable(target)} não declara {absent}")
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
    problem = clone(ctx, revision, copy)
    for target in TARGETS:
        problem = problem or publish(ctx, copy, output / target, target)
        problem = problem or unstamped(output / target, revision.commit, target)
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
        f"um executável único por alvo ({', '.join(TARGETS)}), com commit, árvore limpa e alvo",
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
