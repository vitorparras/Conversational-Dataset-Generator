"""Requires a reason on every `# noqa` that silences a ruff security rule (S) in the service.

Like a semgrep dispensation, the comment names the rule and says why in three words or more.
"""

from __future__ import annotations

import re
from pathlib import Path

from steps.semgrep_suppression import MIN_REASON_WORDS

NOQA = re.compile(r"#\s*noqa:(?P<codes>(?:[\s,]*[A-Z]+\d+)+)(?P<reason>.*)", re.IGNORECASE)
FILE_WIDE = re.compile(r"#\s*(?:ruff|flake8)\s*:\s*noqa", re.IGNORECASE)
SECURITY_CODE = re.compile(r"(?<![A-Z])S\d")
SOURCE_FOLDERS = ("src", "tests", "tools")


def unexplained_in(text: str, name: str) -> list[str]:
    """The lines of one source that silence the whole file, or an S rule without a reason."""
    lines = list(enumerate(text.splitlines(), start=1))
    found = [(n, NOQA.search(line)) for n, line in lines]
    return [
        *(
            f"{name}:{n}: `ruff: noqa` cala o arquivo inteiro"
            for n, line in lines
            if FILE_WIDE.search(line)
        ),
        *(
            f"{name}:{n}: `# noqa` de regra S sem motivo em pelo menos três palavras"
            for n, match in found
            if match and SECURITY_CODE.search(match["codes"])
            if len(match["reason"].split()) < MIN_REASON_WORDS
        ),
    ]


def unexplained(root: Path, folder: str) -> list[str]:
    """Every unexplained S `# noqa` in the sources, tests and tools of ``folder``."""
    problems: list[str] = []
    for sub in SOURCE_FOLDERS:
        for path in sorted((root / folder / sub).rglob("*.py")):
            text = path.read_text(encoding="utf-8", errors="replace")
            problems += unexplained_in(text, path.relative_to(root).as_posix())
    return problems
