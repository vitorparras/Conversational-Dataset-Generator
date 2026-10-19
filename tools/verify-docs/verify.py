#!/usr/bin/env python3
"""Verifies the repository documentation. Runs in CI on every pull request.

Seven checks, in this order of severity:

1. data        - personal data and secrets: document ids, contacts, keys, machine paths
2. references  - a path cited in the documentation that does not exist in the repository
3. links       - an internal link that does not resolve
4. authority   - every document says whether it binds or not
5. formatting  - simple consistency rules
6. size        - line ceiling per document
7. index       - every document appears in the index of its own folder

Usage:
    python tools/verify-docs/verify.py                    # everything
    python tools/verify-docs/verify.py --only data,links
    python tools/verify-docs/verify.py --list             # names of the checks
    python tools/verify-docs/verify.py --root some/dir    # verify another tree

No external dependency: standard library only.
"""

from __future__ import annotations

import argparse
import re
import sys
from collections.abc import Callable
from pathlib import Path

from checks.data import check_data
from checks.links import check_links
from checks.references import check_references
from document import list_documents
from finding import Finding

#: Default root: the repository that contains this script (tools/verify-docs/verify.py).
DEFAULT_ROOT = Path(__file__).resolve().parents[2]

#: Community files whose authority is obvious from the name.
COMMUNITY_FILES = {
    "README.md",
    "CONTRIBUTING.md",
    "SECURITY.md",
    "CODE_OF_CONDUCT.md",
    "CLAUDE.md",
    "AGENTS.md",
}

# ---------------------------------------------------------------------------
# Authority line vocabulary
# ---------------------------------------------------------------------------
# The repository uses plain markdown, with no metadata header. The authority of
# each document lives in a quote line right after the title. The vocabulary is
# closed and is part of the documents' language, so the values stay in Portuguese;
# the writing rules are in docs/regras-de-escrita.md.

AUTHORITIES = (
    "Norma vigente",  # binds
    "Norma substituída por",  # no longer valid; the successor says what is
    "Norma revogada",  # no longer valid, and nothing replaces it
    "Norma pendente",  # still to be decided; the document says what is unknown
    "Decisão vigente",  # binds (used in docs/decisoes/)
    "Decisão substituída por",
    "Decisão revogada",
    "Decisão pendente",  # still to be decided; the document says what is unknown
    "Descrição",  # describes the system; if it diverges, the code wins
    "Histórico",  # zero authority; never a rule
    "Operacional",  # procedure to follow
)

COLUMN_LIMIT = 100
LINE_LIMIT = 60
BLANK_LINES_LIMIT = 2


def _finding(root: Path, path: Path, line: int, check: str, message: str) -> Finding:
    return Finding(path.relative_to(root), line, check, message)


# ---------------------------------------------------------------------------
# 4. Authority line
# ---------------------------------------------------------------------------


def check_authority(root: Path, path: Path, lines: list[str]) -> list[Finding]:
    """Every document declares, in a quote after the title, whether it binds or not.

    With no metadata header, this line is the only place where authority is
    explicit - and without it, history can be read as a rule.
    """
    if path.name in COMMUNITY_FILES:
        return []

    for number, line in enumerate(lines[:12], start=1):
        if line.startswith(">"):
            text = line.lstrip("> ").strip()
            if text.startswith(AUTHORITIES):
                return []
            return [
                _finding(
                    root, path, number, "authority", f"autoridade não reconhecida: {text[:48]!r}"
                )
            ]
    return [
        _finding(root, path, 1, "authority", "falta a linha de autoridade (citação após o título)")
    ]


# ---------------------------------------------------------------------------
# 5. Formatting
# ---------------------------------------------------------------------------


def check_formatting(root: Path, path: Path, lines: list[str]) -> list[Finding]:
    """Simple consistency rules: column, trailing space, blank lines, tab."""
    findings: list[Finding] = []
    consecutive_blank = 0

    for number, line in enumerate(lines, start=1):
        content = line.rstrip("\n")

        if "\t" in content:
            findings.append(_finding(root, path, number, "formatting", "tab: use espaços"))

        if content != content.rstrip():
            findings.append(_finding(root, path, number, "formatting", "espaço no fim da linha"))

        # Tables, long links and code lines do not fit the limit without hurting reading.
        is_exception = content.lstrip().startswith(("|", "    ")) or "](" in content
        if len(content) > COLUMN_LIMIT and not is_exception:
            findings.append(
                _finding(
                    root,
                    path,
                    number,
                    "formatting",
                    f"{len(content)} colunas (limite {COLUMN_LIMIT})",
                )
            )

        if not content.strip():
            consecutive_blank += 1
            if consecutive_blank == BLANK_LINES_LIMIT + 1:
                findings.append(
                    _finding(root, path, number, "formatting", "3+ linhas vazias seguidas")
                )
        else:
            consecutive_blank = 0

    if lines and not lines[-1].endswith("\n"):
        findings.append(
            _finding(root, path, len(lines), "formatting", "arquivo sem quebra de linha final")
        )
    return findings


# ---------------------------------------------------------------------------
# 6. Size
# ---------------------------------------------------------------------------


def check_size(root: Path, path: Path, lines: list[str]) -> list[Finding]:
    """A document above the ceiling is over-justifying itself (writing rule 2).

    Two exceptions. A historical document is neither split nor summarised, because
    that would falsify the record of what was true when it was written. And a folder
    index grows one line per added document: the ceiling would make it shrink by
    hiding documents, the opposite of what an index exists to do.
    """
    if path.name == "README.md":
        return []
    for line in lines[:12]:
        if line.startswith(">") and line.lstrip("> ").startswith("Histórico"):
            return []
    if len(lines) <= LINE_LIMIT:
        return []
    return [_finding(root, path, len(lines), "size", f"{len(lines)} linhas (teto {LINE_LIMIT})")]


# ---------------------------------------------------------------------------
# 7. Folder indexes
# ---------------------------------------------------------------------------


def check_indexes(root: Path) -> list[Finding]:
    """A document missing from its folder index is a document nobody finds.

    This check is per folder, not per file: it compares what exists on disk with
    what the README lists. The failure it catches is silent - whoever adds a
    document and forgets the index gets no error, and the document exists without
    being reachable by whoever reads the folder. The inverse also holds: an index
    that cites a deleted file sends the reader to a dead link.
    """
    findings: list[Finding] = []
    docs = root / "docs"
    if not docs.is_dir():
        return findings
    for folder in sorted(docs.iterdir()):
        index = folder / "README.md"
        if not folder.is_dir() or not index.exists():
            continue
        on_disk = {
            f.name for f in folder.glob("*.md") if f.name != "README.md" and f.name[0].isdigit()
        }
        if not on_disk:
            continue
        text = index.read_text(encoding="utf-8")
        cited = set(re.findall(r"\]\(([^)]*\.md)\)", text))
        for name in sorted(on_disk - cited):
            findings.append(
                _finding(root, index, 1, "index", f"{name} existe e não está no índice")
            )
        for name in sorted(c for c in cited if c and c[0].isdigit()):
            if name not in on_disk:
                findings.append(
                    _finding(root, index, 1, "index", f"o índice cita {name}, que não existe")
                )
    return findings


CheckFunction = Callable[[Path, Path, list[str]], list[Finding]]


def _per_folder_placeholder(root: Path, path: Path, lines: list[str]) -> list[Finding]:
    return []


CHECKS: dict[str, CheckFunction] = {
    "data": check_data,
    "references": check_references,
    "links": check_links,
    "authority": check_authority,
    "formatting": check_formatting,
    "size": check_size,
    # Per folder, not per file: runs outside the loop, in run().
    "index": _per_folder_placeholder,
}


def run(root: Path, chosen: list[str]) -> tuple[list[Path], list[Finding]]:
    """Runs the chosen checks over every document under the root."""
    documents = list_documents(root)
    findings: list[Finding] = []
    for path in documents:
        lines = path.read_text(encoding="utf-8").splitlines(keepends=True)
        for name in chosen:
            findings.extend(CHECKS[name](root, path, lines))
    if "index" in chosen:
        findings.extend(check_indexes(root))
    return documents, findings


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Verifica a documentação do repositório.")
    parser.add_argument("--only", help="verificações a rodar, separadas por vírgula")
    parser.add_argument("--list", action="store_true", help="lista as verificações")
    parser.add_argument(
        "--root",
        type=Path,
        default=DEFAULT_ROOT,
        help="raiz a verificar (padrão: a raiz deste repositório)",
    )
    args = parser.parse_args(argv)

    if args.list:
        for name in CHECKS:
            print(name)
        return 0

    chosen = list(CHECKS)
    if args.only:
        requested = [n.strip() for n in args.only.split(",") if n.strip()]
        unknown = [n for n in requested if n not in CHECKS]
        if unknown:
            print(f"verificação desconhecida: {', '.join(unknown)}")
            return 2
        chosen = requested

    root = args.root.resolve()
    documents, findings = run(root, chosen)

    if not findings:
        print(f"OK — {len(documents)} documentos, {len(chosen)} verificações, nenhum problema.")
        return 0

    per_check: dict[str, int] = {}
    for finding in findings:
        print(finding)
        per_check[finding.check] = per_check.get(finding.check, 0) + 1

    summary = " · ".join(f"{name}: {total}" for name, total in sorted(per_check.items()))
    print(f"\nFALHOU — {len(findings)} problemas em {len(documents)} documentos ({summary})")
    return 1


if __name__ == "__main__":
    sys.exit(main())
