#!/usr/bin/env python3
"""Assembles the service folder: interpreter, locked dependencies, model and the service itself.

    python tools/build-service/service_folder.py --system S --model M --output DIR

The folder lands in DIR/language-service with the interpreter of the contract, an ORIGIN.json that
says where it came from and a SHA256SUMS manifest. The network is needed only here: the folder runs without it. The interpreter comes from
`uv python install`, which installs for the machine it runs on, so the system must be this one.
"""

from __future__ import annotations

import argparse
import os
import re
import shutil
import subprocess
import sys
import tempfile
from collections.abc import Sequence
from dataclasses import dataclass
from pathlib import Path

import origin
import sums
import systems

ROOT = Path(__file__).resolve().parents[2]
SERVICE = "language-service"
PYTHON_VERSION = "3.12.13"
MODEL = re.compile(r"[a-z]{2}_core_news_[a-z]+")
EXTERNALLY_MANAGED = "EXTERNALLY-MANAGED"
UNSET_BYTECODE_VARIABLES = ("PYTHONDONTWRITEBYTECODE", "PYTHONPATH", "PYTHONHOME")
OUTPUT_TAIL_LINES = 15


class BuildError(Exception):
    """A step of the build that failed, in words for whoever runs it."""


FAILURES = (BuildError, origin.OriginError, OSError, ValueError, subprocess.CalledProcessError)


@dataclass(frozen=True)
class Job:
    """What to build: the system, the model by its package name and where the folder goes."""

    system: systems.System
    model: str
    output: Path

    @property
    def folder(self) -> Path:
        return self.output / SERVICE

    @property
    def group(self) -> str:
        """The dependency group of the lock that holds the model, named as uv normalizes it."""
        return self.model.replace("_", "-")


def run(command: Sequence[str | Path], environment: dict[str, str] | None = None) -> None:
    """Runs the command; on failure raises with the tail of what it printed."""
    words = [str(word) for word in command]
    outcome = subprocess.run(words, capture_output=True, text=True, check=False, env=environment)
    if outcome.returncode != 0:
        tail = (outcome.stdout + outcome.stderr).strip().splitlines()[-OUTPUT_TAIL_LINES:]
        raise BuildError("\n".join([f"`{' '.join(words[:3])} ...` falhou", *tail]))


def install_interpreter(store: Path, target: Path) -> None:
    """Installs the pinned interpreter into ``store`` and copies it, whole, to ``target``."""
    run(["uv", "python", "install", "--install-dir", store, PYTHON_VERSION])
    installed = [d for d in store.glob(f"cpython-{PYTHON_VERSION}-*") if not d.is_symlink()]
    if len(installed) != 1:
        raise BuildError(f"o interpretador {PYTHON_VERSION} não apareceu em {store}")
    shutil.copytree(installed[0], target, symlinks=True)
    for marker in target.rglob(EXTERNALLY_MANAGED):
        marker.unlink()


def install_packages(job: Job, interpreter: Path, scratch: Path) -> None:
    """Installs the locked tree with its hashes, then the service itself, into the interpreter."""
    requirements = scratch / "requirements.txt"
    group = [
        "--no-dev",
        "--group",
        job.group,
        "--no-emit-project",
        "--format",
        "requirements-txt",
    ]
    run(
        [
            "uv",
            "export",
            "--frozen",
            *group,
            "--quiet",
            "--output-file",
            requirements,
            "--directory",
            ROOT / SERVICE,
        ]
    )
    install = ["uv", "pip", "install", "--python", interpreter, "--link-mode", "copy"]
    run([*install, "--require-hashes", "--no-deps", "--requirement", requirements])
    run([*install, "--no-deps", ROOT / SERVICE])


def compile_bytecode(interpreter: Path, python: Path) -> None:
    """Rewrites all bytecode as checked-hash, so a copy with new timestamps never invalidates it.

    Without `-f`, compileall skips the bytecode the interpreter itself wrote while starting.
    """
    for cache in list(python.rglob("__pycache__")):
        shutil.rmtree(cache)
    flags = ["-m", "compileall", "-q", "-f", "-j0", "--invalidation-mode", "checked-hash"]
    # Sources with invalid syntax, test data of the standard library, make compileall exit 1.
    subprocess.run([str(interpreter), *flags, str(python)], capture_output=True, check=False)


def smoke(job: Job, interpreter: Path, scratch: Path) -> None:
    """Imports the service and loads the model, then proves that nothing in the manifest moved."""
    program = f"import uvicorn, language_service.app, spacy; spacy.load('{job.model}')"
    environment = {k: v for k, v in os.environ.items() if k not in UNSET_BYTECODE_VARIABLES}
    subprocess.run(  # the working directory is not the folder: nothing may be found by accident
        [str(interpreter), "-c", program], cwd=scratch, env=environment, check=False
    ).check_returncode()
    moved = sums.problems(job.folder)
    if moved:
        raise BuildError(
            "\n".join(["rodar o serviço alterou a pasta:", *moved[:OUTPUT_TAIL_LINES]])
        )


def fill(job: Job, scratch: Path) -> int:
    """Puts everything into the folder, proves it and returns the files its manifest covers."""
    interpreter = job.folder / job.system.interpreter
    declared = origin.read(ROOT, ROOT / SERVICE, job.system.name, PYTHON_VERSION, job.model)
    install_interpreter(scratch / "store", job.folder / "python")
    if not interpreter.is_file():
        raise BuildError(f"o interpretador não está em {job.system.interpreter}")
    install_packages(job, interpreter, scratch)
    compile_bytecode(interpreter, job.folder / "python")
    origin.write(job.folder, declared)
    count = sums.write(job.folder)
    smoke(job, interpreter, scratch)
    return count


def assemble(job: Job) -> int:
    """Builds the folder and returns how many files its manifest covers; a failure leaves none."""
    if job.folder.exists():
        raise BuildError(f"{job.folder} já existe: apague-a ou escolha outra saída")
    try:
        with tempfile.TemporaryDirectory(prefix="cdg-service-", ignore_cleanup_errors=True) as tmp:
            return fill(job, Path(tmp))
    except FAILURES:
        shutil.rmtree(job.folder, ignore_errors=True)
        raise


def parse(argv: Sequence[str]) -> Job:
    parser = argparse.ArgumentParser(description="Monta a pasta do serviço de linguagem.")
    parser.add_argument("--system", required=True, choices=sorted(systems.SYSTEMS))
    parser.add_argument("--model", required=True, help="pacote do modelo, ex.: pt_core_news_md")
    parser.add_argument(
        "--output", required=True, type=Path, help="pasta que recebe language-service/"
    )
    args = parser.parse_args(argv)
    if MODEL.fullmatch(args.model) is None:
        parser.error(f"modelo inválido: {args.model}")
    return Job(systems.SYSTEMS[args.system], args.model, args.output.resolve())


def main(argv: Sequence[str] | None = None) -> int:
    job = parse(sys.argv[1:] if argv is None else argv)
    if not job.system.is_host():
        print(f"esta máquina não é {job.system.name}: o interpretador é o da máquina que monta")
        return 1
    try:
        count = assemble(job)
    except FAILURES as error:
        print(f"a montagem falhou: {error}")
        return 1
    print(f"{job.folder}: {count} arquivos no manifesto")
    print(f"interpretador em {job.system.interpreter}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
