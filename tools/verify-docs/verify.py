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
from dataclasses import dataclass
from pathlib import Path

#: Default root: the repository that contains this script (tools/verify-docs/verify.py).
DEFAULT_ROOT = Path(__file__).resolve().parents[2]

#: Directories that are not project documentation.
IGNORED = {
    ".git", ".github", ".specs", "tools", "node_modules", "bin", "obj",
    # Environments and caches that a build creates inside the repository: never documents of it.
    ".venv", ".pytest_cache", ".hypothesis", "__pycache__",
}

#: Community files whose authority is obvious from the name.
COMMUNITY_FILES = {
    "README.md", "CONTRIBUTING.md", "SECURITY.md", "CODE_OF_CONDUCT.md", "CLAUDE.md", "AGENTS.md",
}

# ---------------------------------------------------------------------------
# Authority line vocabulary
# ---------------------------------------------------------------------------
# The repository uses plain markdown, with no metadata header. The authority of
# each document lives in a quote line right after the title. The vocabulary is
# closed and is part of the documents' language, so the values stay in Portuguese;
# the writing rules are in docs/regras-de-escrita.md.

AUTHORITIES = (
    "Norma vigente",           # binds
    "Norma substituída por",   # no longer valid; the successor says what is
    "Norma revogada",          # no longer valid, and nothing replaces it
    "Norma pendente",          # still to be decided; the document says what is unknown
    "Decisão vigente",         # binds (used in docs/decisoes/)
    "Decisão substituída por",
    "Decisão revogada",
    "Decisão pendente",        # still to be decided; the document says what is unknown
    "Descrição",               # describes the system; if it diverges, the code wins
    "Histórico",               # zero authority; never a rule
    "Operacional",             # procedure to follow
)

# ---------------------------------------------------------------------------
# Personal data and secret patterns (the label is shown to the reader, in Portuguese)
# ---------------------------------------------------------------------------

SENSITIVE_PATTERNS: tuple[tuple[str, str], ...] = (
    (r"\b\d{3}\.\d{3}\.\d{3}-\d{2}\b", "possível CPF"),
    (r"\b\d{2}\.\d{3}\.\d{3}/\d{4}-\d{2}\b", "possível CNPJ"),
    (r"\b\d{2}\.\d{3}\.\d{3}-\d\b", "possível RG"),
    (r"\(\d{2}\)\s?9?\d{4}-\d{4}", "possível telefone"),
    (r"\b[A-Za-z0-9._%+-]+@(?!exemplo\.|example\.)[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b", "e-mail"),
    (r"\b[A-Za-z]:[\\/](?:Users|Repos|Program Files)\b", "caminho absoluto de máquina"),
    (r"/(?:home|Users)/[a-z][a-z0-9_-]+/", "caminho absoluto de máquina"),
    (r"\bgh[pousr]_[A-Za-z0-9]{16,}", "token do GitHub"),
    (r"\bsk-[A-Za-z0-9]{20,}", "chave de API"),
    (r"\bAKIA[0-9A-Z]{16}\b", "chave de acesso AWS"),
    (r"-----BEGIN [A-Z ]*PRIVATE KEY-----", "chave privada"),
)

#: Extensions that, in a cited path, indicate a reference to a repository file.
FILE_EXTENSIONS = (
    ".md", ".cs", ".py", ".csproj", ".sln", ".json", ".yml", ".yaml",
    ".sql", ".cshtml", ".razor", ".toml", ".cff", ".txt",
)

COLUMN_LIMIT = 100
LINE_LIMIT = 60


@dataclass(frozen=True)
class Finding:
    """One problem found, anchored to a file (relative to the root) and a line."""

    file: Path
    line: int
    check: str
    message: str

    def __str__(self) -> str:
        return f"{self.file.as_posix()}:{self.line}  [{self.check}] {self.message}"


def _finding(root: Path, path: Path, line: int, check: str, message: str) -> Finding:
    return Finding(path.relative_to(root), line, check, message)


def list_documents(root: Path) -> list[Path]:
    """Every .md under the root, outside the ignored directories."""
    found: list[Path] = []
    for path in root.rglob("*.md"):
        if any(part in IGNORED for part in path.relative_to(root).parts[:-1]):
            continue
        found.append(path)
    return sorted(found)


def _outside_code_blocks(lines: list[str]) -> list[tuple[int, str]]:
    """Numbered lines (1-based), excluding blocks fenced by ```.

    A code block holds examples - including made-up paths and demo secrets -
    and checking its content would produce false positives.
    """
    outside: list[tuple[int, str]] = []
    inside_block = False
    for number, line in enumerate(lines, start=1):
        if line.lstrip().startswith("```"):
            inside_block = not inside_block
            continue
        if not inside_block:
            outside.append((number, line))
    return outside


# ---------------------------------------------------------------------------
# 1. Personal data and secrets
# ---------------------------------------------------------------------------

def check_data(root: Path, path: Path, lines: list[str]) -> list[Finding]:
    """Looks for personal data, contact, key and machine path patterns.

    Runs over the WHOLE file, code blocks included: a real secret pasted inside
    an example is still a real secret.
    """
    findings: list[Finding] = []
    for number, line in enumerate(lines, start=1):
        for pattern, description in SENSITIVE_PATTERNS:
            if re.search(pattern, line):
                findings.append(
                    _finding(root, path, number, "data", f"{description} nesta linha")
                )
    return findings


# ---------------------------------------------------------------------------
# 2. References to files that do not exist
# ---------------------------------------------------------------------------

def check_references(root: Path, path: Path, lines: list[str]) -> list[Finding]:
    """Checks that every path cited in `inline code` exists in the repository.

    Only what looks like a project file path is considered: it has a slash or a
    known extension. This avoids mistaking a terminal command for a path.
    """
    findings: list[Finding] = []
    for number, line in _outside_code_blocks(lines):
        for snippet in re.findall(r"`([^`\n]+)`", line):
            candidate = snippet.strip().rstrip(":,.;")
            if " " in candidate or candidate.startswith(("-", "$", "http")):
                continue
            looks_like_path = candidate.endswith(FILE_EXTENSIONS) or (
                "/" in candidate and not candidate.endswith("/")
            )
            if not looks_like_path:
                continue
            # A pattern is not a literal path: glob (migrations/*.sql), naming
            # template (NNN-slug.md) and placeholder (<name>, {id}) do not exist
            # as files and must not be charged.
            if "*" in candidate or "NNN" in candidate or "<" in candidate or "{" in candidate:
                continue
            if not (root / candidate).exists():
                findings.append(
                    _finding(root, path, number, "references", f"`{candidate}` não existe")
                )
    return findings


# ---------------------------------------------------------------------------
# 3. Broken internal links
# ---------------------------------------------------------------------------

def check_links(root: Path, path: Path, lines: list[str]) -> list[Finding]:
    """Checks that every relative link resolves. Ignores external links and anchors."""
    findings: list[Finding] = []
    for number, line in _outside_code_blocks(lines):
        for target in re.findall(r"\[[^\]]*\]\(([^)]+)\)", line):
            if target.startswith(("http://", "https://", "mailto:", "#")):
                continue
            resolved = (path.parent / target.split("#", 1)[0]).resolve()
            if not resolved.exists():
                findings.append(
                    _finding(root, path, number, "links", f"link quebrado: {target}")
                )
    return findings


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
                _finding(root, path, number, "authority",
                         f"autoridade não reconhecida: {text[:48]!r}")
            ]
    return [
        _finding(root, path, 1, "authority",
                 "falta a linha de autoridade (citação após o título)")
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
            findings.append(
                _finding(root, path, number, "formatting", "espaço no fim da linha")
            )

        # Tables, long links and code lines do not fit the limit without hurting reading.
        is_exception = content.lstrip().startswith(("|", "    ")) or "](" in content
        if len(content) > COLUMN_LIMIT and not is_exception:
            findings.append(
                _finding(root, path, number, "formatting",
                         f"{len(content)} colunas (limite {COLUMN_LIMIT})")
            )

        if not content.strip():
            consecutive_blank += 1
            if consecutive_blank == 3:
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
    return [
        _finding(root, path, len(lines), "size", f"{len(lines)} linhas (teto {LINE_LIMIT})")
    ]


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
            f.name for f in folder.glob("*.md")
            if f.name != "README.md" and f.name[0].isdigit()
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
        "--root", type=Path, default=DEFAULT_ROOT,
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
