"""The defects the ruler proof plants in a disposable copy, and how each refusal is recognised.

One is a false property added to the service's property suite; one flips the comparison of the
startup secret in a critical module of docs/operacao/014; one adds code no test runs to that same
module, which pulls its coverage under the floor; the last empties the tests that check it, so the
mutants it leaves unchecked survive. Each aims at the service because it costs least, and the
last at a single module so the proof does not pay for the whole mutation matrix. None is ever
planted in the working tree: the callers hand these functions the root of a throwaway copy.
"""

from __future__ import annotations

from collections.abc import Callable
from dataclasses import dataclass
from pathlib import Path

from steps import mutation_service
from steps.areas_doc import DOCUMENT, Area, read_areas
from steps.build_service import SERVICE

MARKER = "semeada_pela_prova_da_regua"
PROPERTY_FILE = f"{SERVICE}/tests/properties/test_{MARKER}.py"
DEFECT_FILE = f"{SERVICE}/src/language_service/auth.py"
SOUND_COMPARISON = "return hmac.compare_digest("
FLIPPED_COMPARISON = f"return not hmac.compare_digest(  # {MARKER}\n        "
FAILED_MARK = "FAILED"
BROKE_MARK = "falharam"
CRITICAL_MODULE = "language_service.auth"
CRITICAL_AREA = "Autenticação do serviço"
BELOW_MARK = "abaixo do piso"
CEILING_MARK = "de sobreviventes passa do limite"
WEAK_TESTS = f"{SERVICE}/tests"
FALSE_PROPERTY = f'''"""Planted by the ruler proof in a throwaway copy; it asserts a falsehood."""

import pytest

from language_service.auth import SecretVerifier


@pytest.mark.property
def test_{MARKER}() -> None:
    assert SecretVerifier.from_secret(b"secret").accepts(b"another secret")
'''
UNCOVERED_FUNCTION = f'''

def {MARKER}() -> int:
    """Planted by the ruler proof in a throwaway copy; no test calls it."""
    first = 1
    second = first + 1
    third = second + 1
    fourth = third + 1
    fifth = fourth + 1
    sixth = fifth + 1
    return sixth
'''
WEAK_TEST = '''"""Planted by the ruler proof; it builds the verifier and asserts nothing."""

from language_service import auth


def test_builds_the_verifier() -> None:
    auth.SecretVerifier.from_secret(b"secret")
'''

Plant = Callable[[Path], str | None]
Touches = Callable[[Path], tuple[str, ...]]
Recognise = Callable[[str], bool]


@dataclass(frozen=True)
class Seed:
    """A defect: what it is, where it goes, how to plant it and how its refusal reads.

    ``touches`` names the files of the copy the planting may change, so they can be put back;
    ``module`` narrows the mutation step to one module; ``platform`` is the only system the
    mechanism that refuses it runs on, None when it runs anywhere.
    """

    case: str
    what: str
    where: str
    plant: Plant
    touches: Touches
    mechanisms: tuple[str, ...]
    recognise: Recognise
    module: str | None = None
    platform: str | None = None


def critical_area(copy: Path) -> Area | str:
    """The area of the module the defects aim at as the copy's document declares it, or why not."""
    table = read_areas(copy)
    area = next((a for a in table.areas if a.prefix == CRITICAL_MODULE), None)
    if table.problems or area is None:
        return f"`{CRITICAL_MODULE}` não é uma área de {DOCUMENT}"
    if area.name != CRITICAL_AREA or area.floor is None or not area.critical:
        return (
            f"{DOCUMENT}: `{CRITICAL_MODULE}` não é mais crítica e com piso, como `{CRITICAL_AREA}`"
        )
    return area


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


def plant_uncovered(copy: Path) -> str | None:
    """Appends to the module a function no test calls; None, or why it was not appended."""
    area = critical_area(copy)
    target = copy / DEFECT_FILE
    if isinstance(area, str) or not target.is_file():
        return area if isinstance(area, str) else f"{DEFECT_FILE} não existe na cópia"
    source = target.read_text(encoding="utf-8")
    target.write_text(
        source.rstrip("\n") + "\n" + UNCOVERED_FUNCTION, encoding="utf-8", newline="\n"
    )
    return None


def weak_test_files(copy: Path) -> tuple[str, ...]:
    """The test files of the copy that import the module, from the repository root; none when the
    module is not an area of the copy's document."""
    area = critical_area(copy)
    if isinstance(area, str):
        return ()
    table = read_areas(copy)
    return tuple(
        f"{SERVICE}/{name}" for name in mutation_service.test_files(copy, area, table.areas)
    )


def plant_weak_tests(copy: Path) -> str | None:
    """Empties every test that imports the module, keeping the import so mutmut still picks it;
    None, or why there was no test to empty."""
    area = critical_area(copy)
    if isinstance(area, str):
        return area
    files = weak_test_files(copy)
    if not files:
        return f"nenhum teste de {SERVICE}/tests importa `{CRITICAL_MODULE}`"
    for name in files:
        (copy / name).write_text(WEAK_TEST, encoding="utf-8", newline="\n")
    return None


def names_the_property(line: str) -> bool:
    """A pytest failure line that cites the planted property; a collection error does not."""
    return FAILED_MARK in line and MARKER in line


def reports_failures(line: str) -> bool:
    """A line of a step that says tests or properties failed, not that the command broke."""
    return BROKE_MARK in line


def names_the_area_below_floor(line: str) -> bool:
    """A line of the floors step that names the area and says it measured under its floor."""
    return CRITICAL_AREA in line and BELOW_MARK in line


def names_the_survivors_over_ceiling(line: str) -> bool:
    """A line of the mutation step that names the module and says its survivors passed the limit;
    a command that broke or a module with no test says neither."""
    return f"`{CRITICAL_MODULE}`" in line and CEILING_MARK in line


def only(*paths: str) -> Touches:
    return lambda _copy: paths


SEEDS: tuple[Seed, ...] = (
    Seed(
        "propriedade falsa",
        f"propriedade `test_{MARKER}` que afirma que o verificador aceita outro segredo",
        PROPERTY_FILE,
        plant_property,
        only(PROPERTY_FILE),
        ("properties",),
        names_the_property,
    ),
    Seed(
        "defeito em módulo crítico",
        "comparação do segredo invertida em `language_service.auth`",
        DEFECT_FILE,
        plant_defect,
        only(DEFECT_FILE),
        ("test_service", "properties"),
        reports_failures,
    ),
    Seed(
        "cobertura abaixo do piso",
        "função sem teste acrescentada a `language_service.auth`",
        DEFECT_FILE,
        plant_uncovered,
        only(DEFECT_FILE),
        ("test_service", "floors"),
        names_the_area_below_floor,
    ),
    Seed(
        "mutantes sobreviventes",
        "testes de `language_service.auth` esvaziados: constroem o verificador e não afirmam nada",
        WEAK_TESTS,
        plant_weak_tests,
        weak_test_files,
        ("mutation",),
        names_the_survivors_over_ceiling,
        CRITICAL_MODULE,
        mutation_service.LINUX,
    ),
)
