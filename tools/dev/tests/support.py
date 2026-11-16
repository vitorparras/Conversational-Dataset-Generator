"""A synthetic git repository and a context to run the steps of check.py against it. The git and
file helpers are the ones of the verifier's tests, loaded under their own name because both test
packages are called `tests`."""

from __future__ import annotations

import importlib.util
import sys
from pathlib import Path

from steps.common import Context

SHARED = Path(__file__).resolve().parents[2] / "verify-code" / "tests" / "support.py"
_spec = importlib.util.spec_from_file_location("verify_code_support", SHARED)
assert _spec is not None and _spec.loader is not None
_shared = importlib.util.module_from_spec(_spec)
sys.modules["verify_code_support"] = _shared
_spec.loader.exec_module(_shared)

git = _shared.git
write = _shared.write


def repository(folder: Path) -> Path:
    """An empty repository whose branch is `main`, with one commit."""
    git(folder, "init", "-q", "-b", "main")
    _shared.write(folder, "README.md", "# x\n")
    git(folder, "add", "-A")
    git(folder, "commit", "-q", "-m", "chore: início")
    return folder


def commit(repo: Path, files: dict[str, str], title: str) -> None:
    """Writes the files and commits them with ``title``."""
    for name, text in files.items():
        _shared.write(repo, name, text)
    git(repo, "add", "-A")
    git(repo, "commit", "-q", "-m", title)


def context(repo: Path, base: str = "main") -> Context:
    return _shared.context(repo, base=base)
