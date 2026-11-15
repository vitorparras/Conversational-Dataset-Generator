"""A synthetic git repository and a context to run the steps of check.py against it."""

from __future__ import annotations

import subprocess
from pathlib import Path

from steps.common import Context, run_process

IDENTITY = ("-c", "user.name=Teste", "-c", "user.email=teste@example.org")


def git(repo: Path, *args: str) -> str:
    done = subprocess.run(
        ["git", *IDENTITY, *args],
        cwd=repo,
        capture_output=True,
        text=True,
        check=True,
    )
    return done.stdout


def repository(folder: Path) -> Path:
    """An empty repository whose branch is `main`, with one commit."""
    git(folder, "init", "-q", "-b", "main")
    (folder / "README.md").write_text("# x\n", encoding="utf-8")
    git(folder, "add", "-A")
    git(folder, "commit", "-q", "-m", "chore: início")
    return folder


def commit(repo: Path, files: dict[str, str], title: str) -> None:
    """Writes the files and commits them with ``title``."""
    for name, text in files.items():
        path = repo / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8", newline="\n")
    git(repo, "add", "-A")
    git(repo, "commit", "-q", "-m", title)


def context(repo: Path, base: str = "main") -> Context:
    return Context(repo, repo / ".verify-code", run_process, base)
