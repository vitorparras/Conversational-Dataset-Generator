"""Personal data and secrets."""

from __future__ import annotations

import re
from pathlib import Path

from finding import Finding, Location

# The label is shown to the reader, so it stays in Portuguese.
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


def check_data(root: Path, path: Path, lines: list[str]) -> list[Finding]:
    """Looks for personal data, contact, key and machine path patterns.

    Runs over the WHOLE file, code blocks included: a real secret pasted inside
    an example is still a real secret.
    """
    return [
        Finding.at(Location(root, path, number), "data", f"{description} nesta linha")
        for number, line in enumerate(lines, start=1)
        for pattern, description in SENSITIVE_PATTERNS
        if re.search(pattern, line)
    ]
