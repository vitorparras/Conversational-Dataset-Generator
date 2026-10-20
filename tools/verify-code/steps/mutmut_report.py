"""Reads what mutmut left in its workspace: the status of each mutant from its `.meta` files and,
for a survivor, the line and the change from the diff that `mutmut show` prints."""

from __future__ import annotations

import json
import re
from dataclasses import dataclass
from pathlib import Path

from steps.mutant import Mutant, MutantStatus

MUTANTS = "mutants"
META = ".meta"
SURVIVED = frozenset({0, 5, 33})
UNCHECKED = frozenset({2, 34})
NUMBER = re.compile(r"__mutmut_\d+$")
CLASS_SEPARATOR = "ǁ"
UNREAD = "alteração não lida: `mutmut show` não mostrou o diff"


@dataclass(frozen=True)
class Entry:
    """A mutant by name, its file inside the workspace and the exit code its test run ended with."""

    name: str
    file: str
    code: int | None


def status_of(code: int | None) -> MutantStatus:
    """Survived when the tests passed or none reached it; unchecked when it never ran."""
    if code is None or code in UNCHECKED:
        return MutantStatus.UNCHECKED
    return MutantStatus.SURVIVED if code in SURVIVED else MutantStatus.KILLED


def entries(workspace: Path) -> tuple[Entry, ...]:
    """Every mutant the `.meta` files of the workspace record, file by file."""
    folder = workspace / MUTANTS
    return tuple(
        Entry(name, meta.relative_to(folder).as_posix().removesuffix(META), code)
        for meta in sorted(folder.rglob(f"*.py{META}"))
        for name, code in json.loads(meta.read_text(encoding="utf-8"))["exit_code_by_key"].items()
    )


def scope_of(name: str) -> tuple[str, ...]:
    """Class and function a name points at: `pkg.mod.x_f__mutmut_3` or `pkg.mod.xǁCǁf__mutmut_3`."""
    last = NUMBER.sub("", name).rsplit(".", 1)[-1]
    parts = last.split(CLASS_SEPARATOR)[1:] if CLASS_SEPARATOR in last else [last[2:]]
    return tuple(parts)


def changed(shown: str, sign: str) -> str:
    """The first line the diff marks with ``sign``, without the mark, the file header left out."""
    lines = shown.splitlines()
    marked = (line for line in lines if line.startswith(sign) and not line.startswith(sign * 3))
    return next((line[1:].strip() for line in marked), "")


def located(source: list[str], scope: tuple[str, ...], removed: str) -> int:
    """The line of ``removed`` inside the class and function of ``scope``; 0 when not found."""
    start = 1
    for name in scope:
        header = re.compile(rf"\s*(?:async\s+)?(?:def|class) {re.escape(name)}\b")
        start = next((n for n, t in enumerate(source[start - 1 :], start) if header.match(t)), 0)
        start = start or len(source) + 1
    after = enumerate(source[start - 1 :], start)
    return next((n for n, text in after if removed and text.strip() == removed), 0)


def survivor(entry: Entry, shown: str, source: str, folder: str) -> Mutant:
    """The survivor named by file under ``folder``, line and change, from its diff and source."""
    removed, added = changed(shown, "-"), changed(shown, "+")
    line = located(source.splitlines(), scope_of(entry.name), removed)
    change = f"`{removed}` → `{added}`" if removed else UNREAD
    return Mutant(f"{folder}/{entry.file}", line, change, status_of(entry.code))
