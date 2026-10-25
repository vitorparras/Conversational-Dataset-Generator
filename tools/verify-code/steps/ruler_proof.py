"""Proves the ruler refuses what it must: plants known defects in a throwaway copy and expects a no.

The copy is a clone of HEAD in a temporary folder outside the working tree, removed in every case.
A defect counts as refused only when a mechanism fails for the reason the defect gives; a command
that broke, or a suite that ran nothing, is not a refusal. If the ruler accepts a planted defect,
the ruler is what is broken, and the step fails.
"""

from __future__ import annotations

import shutil
import stat
import tempfile
from collections.abc import Callable, Mapping
from dataclasses import dataclass
from pathlib import Path

from steps import package, properties, test_service
from steps.common import Context, StepResult, failed, passed, tail
from steps.ruler_seed import MARKER, SEEDS, Seed

NAME = "ruler_proof"
COPY = "copy"
PARENT_PREFIX = "ruler-proof-"
STATUS = ["git", "status", "--porcelain"]
HEAD = ["git", "rev-parse", "HEAD"]

Mechanism = Callable[[Context], StepResult]
Populate = Callable[[Context, Path], StepResult | None]


@dataclass(frozen=True)
class Kit:
    """What the proof builds on: the mechanisms by name, how the copy is made and where it lives."""

    mechanisms: Mapping[str, Mechanism]
    populate: Populate
    make_parent: Callable[[], Path]


def clone_head(ctx: Context, parent: Path) -> StepResult | None:
    """Fills ``parent`` with a clean clone of the commit the working tree stands on."""
    head = ctx.run(HEAD, ctx.root)
    if not head.ok:
        return failed(NAME, "não foi possível ler o commit atual", *tail(head.text))
    problem = package.clone(ctx, package.Revision(head.text.strip(), None), parent / COPY)
    return None if problem is None else failed(NAME, *problem.details)


def make_parent() -> Path:
    return Path(tempfile.mkdtemp(prefix=PARENT_PREFIX))


DEFAULT = Kit(
    {"properties": properties.run, "test_service": test_service.run},
    clone_head,
    make_parent,
)


def writable_then_retry(function: Callable[[str], object], path: str, _error: object) -> None:
    """Git marks its object files read-only; Windows refuses to delete those until they are not."""
    Path(path).chmod(stat.S_IWRITE)
    function(path)


def discard(parent: Path) -> None:
    shutil.rmtree(parent, onexc=writable_then_retry)


def refusal(seed: Seed, copy: Path, ctx: Context, kit: Kit) -> tuple[str | None, list[str]]:
    """The mechanism that refused the planted defect with its line, or None, and what was tried."""
    copy_ctx = Context(copy, copy / ".verify-code", ctx.run, ctx.base, ctx.previous, ctx.run_for)
    tried: list[str] = []
    for name in seed.mechanisms:
        result = kit.mechanisms[name](copy_ctx)
        line = next((d.strip(" |") for d in result.details if seed.recognise(d)), None)
        if not result.ok and line is not None:
            return f"{name}: {line}", tried
        tried.append(f"{name}: " + (result.details[0] if result.details else "sem relato"))
    return None, tried


def restore(target: Path, original: bytes | None) -> None:
    """Puts ``target`` back as it was: its old bytes, or gone if the seed created it."""
    if original is None:
        target.unlink(missing_ok=True)
    else:
        target.write_bytes(original)


def judge_seed(seed: Seed, copy: Path, ctx: Context, kit: Kit) -> tuple[bool, list[str]]:
    """Plants one defect in the copy, runs its mechanisms and reports in the format of the issue.

    The file is put back afterwards, so the next defect meets the copy as the first one did.
    """
    target = copy / seed.where
    original = target.read_bytes() if target.is_file() else None
    problem = seed.plant(copy)
    if problem is not None:
        return False, [f"{seed.case}: a semente não pôde ser plantada: {problem}"]
    try:
        refused_by, tried = refusal(seed, copy, ctx, kit)
    finally:
        restore(target, original)
    planted = f"semeado: {seed.what}, em {seed.where}"
    if refused_by is None:
        return False, [f"a régua aceitou {seed.case}", planted, *(f"  aceito: {t}" for t in tried)]
    return True, [seed.case, f"  {planted}", f"  recusado por: {refused_by}"]


def traces(ctx: Context, before: str, parent: Path) -> list[str]:
    """What the proof left behind: a changed working tree, the copy, or the marker in the output."""
    after = ctx.run(STATUS, ctx.root).text
    found = [] if after == before else ["a árvore de trabalho mudou durante a prova"]
    found += [f"{parent} ainda existe"] if parent.exists() else []
    leaked = ctx.out_dir.is_dir() and any(
        MARKER.encode() in path.read_bytes() for path in ctx.out_dir.rglob("*") if path.is_file()
    )
    return found + ([f"`{MARKER}` apareceu em {ctx.out_dir.name}/"] if leaked else [])


def prove(ctx: Context, kit: Kit) -> StepResult:
    """Plants every seed in one throwaway copy; passes only if each is refused and nothing stays."""
    status = ctx.run(STATUS, ctx.root)
    if not status.ok:
        return failed(NAME, "não foi possível ler o estado da árvore", *tail(status.text))
    parent = kit.make_parent()
    try:
        problem = kit.populate(ctx, parent)
        verdicts = [] if problem else [judge_seed(s, parent / COPY, ctx, kit) for s in SEEDS]
    finally:
        discard(parent)
    left = traces(ctx, status.text, parent)
    lines = [line for _, report in verdicts for line in report]
    if problem is not None or left or not all(ok for ok, _ in verdicts):
        return failed(NAME, *(problem.details if problem else ()), *lines, *left)
    return passed(NAME, *lines)


def run(ctx: Context) -> StepResult:
    return prove(ctx, DEFAULT)
