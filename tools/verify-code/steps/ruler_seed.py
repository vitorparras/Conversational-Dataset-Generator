"""The two defects the ruler proof plants in a disposable copy, and how each refusal is recognised.

One is a false property added to the service's property suite; the other flips the comparison of
the startup secret in a critical module of docs/operacao/014. Neither is ever planted in the
working tree: the callers hand these functions the root of a throwaway copy.
"""

from __future__ import annotations

from collections.abc import Callable
from dataclasses import dataclass
from pathlib import Path

from steps.build_service import SERVICE

MARKER = "semeada_pela_prova_da_regua"
PROPERTY_FILE = f"{SERVICE}/tests/properties/test_{MARKER}.py"
DEFECT_FILE = f"{SERVICE}/src/language_service/auth.py"
SOUND_COMPARISON = "return hmac.compare_digest("
FLIPPED_COMPARISON = f"return not hmac.compare_digest(  # {MARKER}\n        "
FAILED_MARK = "FAILED"
BROKE_MARK = "falharam"
FALSE_PROPERTY = f'''"""Planted by the ruler proof in a throwaway copy; it asserts a falsehood."""

import pytest

from language_service.auth import SecretVerifier


@pytest.mark.property
def test_{MARKER}() -> None:
    assert SecretVerifier.from_secret(b"secret").accepts(b"another secret")
'''

Plant = Callable[[Path], str | None]
Recognise = Callable[[str], bool]


@dataclass(frozen=True)
class Seed:
    """A defect: what it is, where it goes, how to plant it and how its refusal reads."""

    case: str
    what: str
    where: str
    plant: Plant
    mechanisms: tuple[str, ...]
    recognise: Recognise


def plant_property(copy: Path) -> str | None:
    """Adds the false property to the copy; None, or why it could not be added."""
    target = copy / PROPERTY_FILE
    if not target.parent.is_dir():
        return f"{target.parent.relative_to(copy).as_posix()}/ não existe na cópia"
    target.write_text(FALSE_PROPERTY, encoding="utf-8", newline="\n")
    return None


def plant_defect(copy: Path) -> str | None:
    """Flips the secret comparison in the copy; None, or why the line was not found once."""
    target = copy / DEFECT_FILE
    source = target.read_text(encoding="utf-8") if target.is_file() else ""
    if source.count(SOUND_COMPARISON) != 1:
        return f"{DEFECT_FILE}: não há exatamente uma `{SOUND_COMPARISON}...`"
    target.write_text(source.replace(SOUND_COMPARISON, FLIPPED_COMPARISON), encoding="utf-8")
    return None


def names_the_property(line: str) -> bool:
    """A pytest failure line that cites the planted property; a collection error does not."""
    return FAILED_MARK in line and MARKER in line


def reports_failures(line: str) -> bool:
    """A line of a step that says tests or properties failed, not that the command broke."""
    return BROKE_MARK in line


SEEDS: tuple[Seed, ...] = (
    Seed(
        "propriedade falsa",
        f"propriedade `test_{MARKER}` que afirma que o verificador aceita outro segredo",
        PROPERTY_FILE,
        plant_property,
        ("properties",),
        names_the_property,
    ),
    Seed(
        "defeito em módulo crítico",
        "comparação do segredo invertida em `language_service.auth`",
        DEFECT_FILE,
        plant_defect,
        ("test_service", "properties"),
        reports_failures,
    ),
)
