#!/usr/bin/env python3
"""Verifies the repository documentation. Runs in CI on every pull request.

Eight checks, in this order of severity:

1. data        - personal data and secrets: document ids, contacts, keys, machine paths
2. references  - a path cited in the documentation that does not exist in the repository
3. identifiers - an identifier cited in `inline code` that does not exist in the code
4. links       - an internal link that does not resolve
5. authority   - every document says whether it binds or not
6. formatting  - simple consistency rules
7. size        - line ceiling per document
8. index       - every document appears in the index of its own folder

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
    (r"/(?:home|Users)/[A-Za-z0-9._-]+/", "caminho absoluto de máquina"),
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
# 2b. Identifiers cited in `inline code` that do not exist in the code
# ---------------------------------------------------------------------------

#: Where the code lives. A repository without any of these has no code to resolve against.
CODE_ROOTS = ("core", "language-service")
CODE_IGNORED = {"bin", "obj", ".venv", "node_modules", "__pycache__", ".pytest_cache", ".hypothesis"}

#: Endings that make an inline snippet a file name, not an identifier (`Program.cs`, `Directory.Build.props`).
NON_IDENTIFIER_SUFFIXES = FILE_EXTENSIONS + (
    ".props", ".targets", ".slnx", ".xml", ".sh", ".ps1", ".ini", ".cfg", ".lock", ".dll", ".exe",
    ".html", ".css", ".js", ".ts", ".csv", ".log", ".env", ".gitignore", ".editorconfig",
)

_IDENTIFIER = re.compile(r"^[A-Za-z_][A-Za-z0-9_.]*$")
_CONFIGURATION_KEY = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*(?::[A-Za-z_][A-Za-z0-9_]*)+$")
_NAME = re.compile(r"[A-Za-z_][A-Za-z0-9_]*")

# One pass over C# source, so a quote inside a comment and a slash inside a string do not confuse
# each other. Order matters: raw string, verbatim string, string, character, then comments.
_CSHARP_SKIPPED = re.compile(
    r'"""(?P<raw>.*?)"""'
    r'|@"(?P<verbatim>(?:[^"]|"")*)"'
    r'|"(?P<string>(?:[^"\\\n]|\\.)*)"'
    r"|'(?:[^'\\\n]|\\.)*'"
    r"|//[^\n]*"
    r"|/\*.*?\*/",
    re.DOTALL,
)


class CodeIndex:
    """What the code contains: the names it uses, and the string literals it holds.

    A name counts when it appears as a name in code (not in a comment or a string), which is what
    a declaration and every use of it have in common. A renamed identifier disappears from all of
    them at once, so the old name stops being found; that is the failure this check exists for.
    """

    def __init__(self, root: Path) -> None:
        self.names: set[str] = set()
        self.literals: set[str] = set()
        self.available = False
        for folder in CODE_ROOTS:
            base = root / folder
            if not base.is_dir():
                continue
            self.available = True
            for path in sorted(base.rglob("*")):
                relative = path.relative_to(base).parts
                if not path.is_file() or any(part in CODE_IGNORED for part in relative):
                    continue
                self.names.update(_NAME.findall(path.stem))
                self.names.update(_NAME.findall(" ".join(relative[:-1])))
                if path.suffix == ".cs":
                    self._read_csharp(path)
                elif path.suffix == ".py":
                    self._read_python(path)
                elif path.suffix == ".json":
                    self._read_json(path)

    def _read_csharp(self, path: Path) -> None:
        text = path.read_text(encoding="utf-8", errors="replace")
        code: list[str] = []
        last = 0
        for match in _CSHARP_SKIPPED.finditer(text):
            code.append(text[last:match.start()])
            literal = match.group("raw") or match.group("verbatim") or match.group("string")
            if literal:
                self.literals.add(literal)
            code.append(" ")
            last = match.end()
        code.append(text[last:])
        self.names.update(_NAME.findall("".join(code)))

    def _read_python(self, path: Path) -> None:
        import io
        import tokenize

        text = path.read_text(encoding="utf-8", errors="replace")
        try:
            for token in tokenize.generate_tokens(io.StringIO(text).readline):
                if token.type == tokenize.NAME:
                    self.names.add(token.string)
                elif token.type == tokenize.STRING:
                    self.literals.add(token.string.strip("\"'rbfRBF"))
        except (tokenize.TokenError, IndentationError, SyntaxError):
            self.names.update(_NAME.findall(text))

    def _read_json(self, path: Path) -> None:
        self.literals.update(re.findall(r'"([^"\n]+)"\s*:', path.read_text(encoding="utf-8", errors="replace")))

    def has_name(self, name: str) -> bool:
        return name in self.names

    def has_literal(self, text: str) -> bool:
        """The text is a string the code holds, a key of a JSON file, or a name used in code."""
        return text in self.names or text in self.literals or any(text in literal for literal in self.literals)


_INDEX_CACHE: dict[Path, CodeIndex] = {}


def code_index(root: Path) -> CodeIndex:
    """The index of the code under ``root``, built once per run."""
    if root not in _INDEX_CACHE:
        _INDEX_CACHE[root] = CodeIndex(root)
    return _INDEX_CACHE[root]


def cited_identifier(snippet: str) -> tuple[str, str] | None:
    """The kind and the text of what an `inline code` snippet cites, or None when it cites no identifier.

    ``name``: a dotted identifier with an uppercase letter, an underscore or a dot (``ClosedConfiguration.Load``,
    ``language_service.auth``), every part of which must exist. ``literal``: a configuration key
    (``Section:Key``) or a snake_case field, searched as text. Anything with a slash, a space, a file
    extension or no distinguishing mark (a common word) is not an identifier.
    """
    text = snippet.strip()
    if text.endswith("()"):
        text = text[:-2]
    if not text or " " in text or "/" in text or "\\" in text:
        return None
    if text.endswith(NON_IDENTIFIER_SUFFIXES) or text.startswith(("-", "$", "http", ".")):
        return None
    if _CONFIGURATION_KEY.match(text):
        return "literal", text
    if not _IDENTIFIER.match(text) or text.endswith("."):
        return None
    if text.islower() and "_" in text and "." not in text:
        return "literal", text
    if "." not in text and "_" not in text:
        # A bare word names code only when it is written like a name: camelCase or PascalCase with
        # two humps or more. A single capitalised word is as likely to be Portuguese, and an
        # all-capitals word is a keyword or an acronym (FORCE, HMAC), never an identifier to confirm.
        if text.isupper() or sum(c.isupper() for c in text) < 2:
            return None
    elif "." not in text and text.isupper() and re.search(r"_(?:[0-9]+|[A-Z])$", text):
        return None  # a placeholder in an example (PESSOA_1, REMETENTE_A), not a constant
    elif not any(c.isupper() for c in text) and "_" not in text:
        return None
    return "name", text


def check_identifiers(root: Path, path: Path, lines: list[str]) -> list[Finding]:
    """Checks that every identifier cited in `inline code` exists in the code.

    A fenced block is an example, not a citation, and is skipped. The document is not required to
    give the path of the file that holds the identifier. Findings are per document and line, and
    every document is checked, so a renamed identifier lists all the documents that cite it.
    """
    index = code_index(root)
    if not index.available:
        return []
    findings: list[Finding] = []
    for number, line in _outside_code_blocks(lines):
        for snippet in re.findall(r"`([^`\n]+)`", line):
            cited = cited_identifier(snippet)
            if cited is None:
                continue
            kind, text = cited
            if kind == "literal":
                found = all(index.has_literal(part) for part in text.split(":"))
            else:
                found = all(index.has_name(part) for part in text.split("."))
            if not found:
                findings.append(
                    _finding(root, path, number, "identifiers", f"`{text}` não existe no código")
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
    "identifiers": check_identifiers,
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
