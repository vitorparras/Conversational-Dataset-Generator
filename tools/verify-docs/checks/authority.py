"""The authority line: whether each document binds or not."""

from __future__ import annotations

from pathlib import Path

from finding import Finding, Location

#: Community files whose authority is obvious from the name.
COMMUNITY_FILES = {
    "README.md",
    "CONTRIBUTING.md",
    "SECURITY.md",
    "CODE_OF_CONDUCT.md",
    "CLAUDE.md",
    "AGENTS.md",
}

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


def _first_quote(lines: list[str]) -> tuple[int, str] | None:
    for number, line in enumerate(lines[:12], start=1):
        if line.startswith(">"):
            return number, line.lstrip("> ").strip()
    return None


def check_authority(root: Path, path: Path, lines: list[str]) -> list[Finding]:
    """Every document declares, in a quote after the title, whether it binds or not.

    With no metadata header, this line is the only place where authority is
    explicit - and without it, history can be read as a rule.
    """
    if path.name in COMMUNITY_FILES:
        return []
    quote = _first_quote(lines)
    if quote is None:
        message = "falta a linha de autoridade (citação após o título)"
        return [Finding.at(Location(root, path, 1), "authority", message)]
    number, text = quote
    if text.startswith(AUTHORITIES):
        return []
    message = f"autoridade não reconhecida: {text[:48]!r}"
    return [Finding.at(Location(root, path, number), "authority", message)]
