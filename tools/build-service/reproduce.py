#!/usr/bin/env python3
"""Proves that the service folder is rebuilt equal: two clean copies of HEAD, two builds, compared.

    python tools/build-service/reproduce.py --system S --model M

Each copy is a clone of the commit in a temporary folder, never the working tree, so a change not
committed cannot reach the folder. The first build has the network and fills the cache of `uv`; the
second runs with `UV_OFFLINE` and a proxy that points at a closed port, so it only finds what the
first one fetched. Then every file of the two folders is compared byte by byte, and every item that
differs is named. A difference only passes if JUSTIFIED holds its name, with the reason, and the
reason is in docs/operacao/018; the files that do not differ are compared as always.
"""

from __future__ import annotations

import argparse
import os
import subprocess
import sys
import tempfile
import time
from collections.abc import Mapping, Sequence
from dataclasses import dataclass
from pathlib import Path

import origin
import service_folder
import sums
import systems

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = Path(__file__).resolve().with_name("service_folder.py")
CLOSED_PORT_PROXY = "http://127.0.0.1:9"
PROXIES = ("HTTPS_PROXY", "https_proxy", "HTTP_PROXY", "http_proxy")
NO_PROXY = ("NO_PROXY", "no_proxy")
ARCHIVES = "UV_PYTHON_CACHE_DIR"
TAIL_LINES = 15
# path of a file relative to the folder -> why it may differ; empty: every byte is reproduced
JUSTIFIED: Mapping[str, str] = {}


class ReproductionError(Exception):
    """A step of the proof that failed, in words for whoever runs it."""


@dataclass(frozen=True)
class Comparison:
    """What the two folders hold: files in both, and the names that differ or exist in one only."""

    equal: int
    different: tuple[str, ...]
    only_first: tuple[str, ...]
    only_second: tuple[str, ...]

    @property
    def unjustified(self) -> tuple[str, ...]:
        """Every item that differs and has no reason in JUSTIFIED."""
        names = (*self.different, *self.only_first, *self.only_second)
        return tuple(name for name in names if name not in JUSTIFIED)


def command(arguments: Sequence[str | Path], cwd: Path, environment: Mapping[str, str]) -> None:
    """Runs the command; on failure raises with the tail of what it printed."""
    words = [str(word) for word in arguments]
    done = subprocess.run(
        words, cwd=cwd, env=dict(environment), capture_output=True, text=True, check=False
    )
    if done.returncode != 0:
        tail = (done.stdout + done.stderr).strip().splitlines()[-TAIL_LINES:]
        raise ReproductionError("\n".join([f"`{' '.join(words[:4])} ...` falhou", *tail]))


def offline(environment: Mapping[str, str]) -> dict[str, str]:
    """The environment with no way out: `uv` offline and every proxy pointing at a closed port."""
    closed = {name: CLOSED_PORT_PROXY for name in PROXIES}
    return {**environment, **closed, **dict.fromkeys(NO_PROXY, ""), "UV_OFFLINE": "1"}


def clone(commit: str, copy: Path, environment: Mapping[str, str]) -> None:
    """A clone of ``commit`` at ``copy`` that git sees as clean."""
    here = Path.cwd()
    steps = [
        ["git", "clone", "--quiet", "--no-checkout", "--no-hardlinks", ROOT, copy],
        ["git", "-C", copy, "checkout", "--quiet", "--detach", commit],
    ]
    for step in steps:
        command(step, here, environment)
    if origin.tree_state(copy) != origin.CLEAN:
        raise ReproductionError("a cópia não está limpa: a pasta não sairia só do commit")


def build(copy: Path, job: service_folder.Job, environment: Mapping[str, str]) -> float:
    """Builds the folder from the script of ``copy`` and returns how long it took, in seconds."""
    script = copy / SCRIPT.relative_to(ROOT)
    arguments = [sys.executable, script, "--system", job.system.name, "--model", job.model]
    started = time.perf_counter()
    command([*arguments, "--output", job.output], copy, environment)
    return time.perf_counter() - started


def entry(path: Path) -> str:
    """What identifies a file of the folder: its digest, or where a link points."""
    return f"-> {os.readlink(path)}" if path.is_symlink() else sums.digest(path)


def listing(folder: Path) -> dict[str, str]:
    """Every file of ``folder``, the manifest too, with what identifies it."""
    names = [*sums.files(folder), sums.MANIFEST]
    return {name: entry(folder / name) for name in names}


def compare(first: Mapping[str, str], second: Mapping[str, str]) -> Comparison:
    """Splits the files of two listings into equal, different and present in one only."""
    both = first.keys() & second.keys()
    different = tuple(sorted(name for name in both if first[name] != second[name]))
    return Comparison(
        equal=len(both) - len(different),
        different=different,
        only_first=tuple(sorted(first.keys() - second.keys())),
        only_second=tuple(sorted(second.keys() - first.keys())),
    )


def report(comparison: Comparison) -> list[str]:
    """The lines that say what the comparison found, one per item that differs."""
    reason = (
        f"{name}: {JUSTIFIED.get(name, 'SEM justificativa')}" for name in comparison.different
    )
    lines = [f"{comparison.equal} arquivos iguais byte a byte nas duas pastas"]
    lines += [f"difere: {line}" for line in reason]
    lines += [f"só na primeira: {name}" for name in comparison.only_first]
    lines += [f"só na segunda: {name}" for name in comparison.only_second]
    return lines


def prove(job: service_folder.Job, environment: Mapping[str, str]) -> int:
    """Builds twice from clean copies of HEAD, the second offline, and compares the folders."""
    commit = origin.git(ROOT, "rev-parse", "HEAD").strip()
    with tempfile.TemporaryDirectory(prefix="cdg-reproduce-", ignore_cleanup_errors=True) as tmp:
        scratch = Path(tmp)
        folders: list[Path] = []
        # `uv python install` keeps no archive unless told where: the second build needs the first's
        shared = {**environment, ARCHIVES: str(scratch / "python-archives")}
        for turn, network, world in (
            ("primeira", "com rede", shared),
            ("segunda", "sem rede", offline(shared)),
        ):
            copy, output = scratch / f"copy-{turn}", scratch / f"out-{turn}"
            clone(commit, copy, shared)
            seconds = build(copy, service_folder.Job(job.system, job.model, output), world)
            print(f"{turn} montagem {network}: {seconds:.0f} s")
            folders.append(output / service_folder.SERVICE)
        comparison = compare(listing(folders[0]), listing(folders[1]))
    print(f"commit {commit}")
    print("\n".join(report(comparison)))
    return 1 if comparison.unjustified else 0


def parse(argv: Sequence[str]) -> service_folder.Job:
    parser = argparse.ArgumentParser(description="Prova que a pasta do serviço se reproduz.")
    parser.add_argument("--system", required=True, choices=sorted(systems.SYSTEMS))
    parser.add_argument("--model", required=True, help="pacote do modelo, ex.: pt_core_news_md")
    args = parser.parse_args(argv)
    if service_folder.MODEL.fullmatch(args.model) is None:
        parser.error(f"modelo inválido: {args.model}")
    return service_folder.Job(systems.SYSTEMS[args.system], args.model, Path())


def main(argv: Sequence[str] | None = None) -> int:
    job = parse(sys.argv[1:] if argv is None else argv)
    if not job.system.is_host():
        print(f"esta máquina não é {job.system.name}: o interpretador é o da máquina que monta")
        return 1
    try:
        return prove(job, os.environ)
    except (ReproductionError, origin.OriginError, OSError, ValueError) as error:
        print(f"a prova falhou: {error}")
        return 1


if __name__ == "__main__":
    sys.exit(main())
