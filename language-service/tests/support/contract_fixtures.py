"""The JSON files of core/tests/fixtures/contract, which the core tests read as well."""

from pathlib import Path
from typing import Final

FIXTURES: Final = Path("core") / "tests" / "fixtures" / "contract"


def fixture_directory() -> Path:
    """The shared fixture folder, found in the first folder above the tests that holds it."""
    here = Path(__file__).resolve()
    found = next(
        (parent / FIXTURES for parent in here.parents if (parent / FIXTURES).is_dir()), None
    )
    if found is None:
        raise FileNotFoundError(f"{FIXTURES} was not found above the tests")
    return found


def fixture_text(relative_path: str) -> str:
    return (fixture_directory() / relative_path).read_text(encoding="utf-8")
