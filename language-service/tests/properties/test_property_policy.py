"""Decision 0007 and issue 41: how a property is declared, reported and repeated.

A property that fails on purpose runs in a child pytest, under the same plugin the real properties
use, and the tests read what a person reading the pull request would read.
"""

import os
import re
import subprocess
import sys
from pathlib import Path

from support import property_policy

TESTS_DIRECTORY = Path(__file__).resolve().parents[1]

FAILING_PROPERTY = '''
from hypothesis import given
from hypothesis import strategies as st


@given(st.integers(0, 1_000_000))
def test_always_below_eleven(value):
    assert value <= 10, f"value {value} is above ten"
'''


def run_failing_property(tmp_path: Path, seed: str | None) -> str:
    """Run the failing property in a child pytest and return everything it printed."""
    (tmp_path / "test_fixture_property.py").write_text(FAILING_PROPERTY, encoding="utf-8")
    (tmp_path / "pytest.ini").write_text("[pytest]" + chr(10), encoding="utf-8")
    environment = {**os.environ, "PYTHONPATH": str(TESTS_DIRECTORY), "PYTHONDONTWRITEBYTECODE": "1"}
    environment.pop(property_policy.SEED_VARIABLE, None)
    if seed is not None:
        environment[property_policy.SEED_VARIABLE] = seed
    completed = subprocess.run(
        [sys.executable, "-m", "pytest", "-p", "support.property_policy", "-p", "no:cacheprovider",
         "--confcutdir", str(tmp_path), str(tmp_path / "test_fixture_property.py")],
        cwd=tmp_path, env=environment, capture_output=True, text=True, timeout=120, check=False,
    )
    assert completed.returncode == 1, completed.stdout + completed.stderr
    return completed.stdout


def counterexample(report: str) -> str:
    """The falsifying value the report names."""
    found = re.search(r"value (\d+) is above ten", report)
    assert found, report
    return found.group(1)


def test_the_profile_declares_the_budget_and_the_blob() -> None:
    """The numbers are declared in one profile, not inherited from a library default."""
    from hypothesis import settings

    active = settings.get_profile(property_policy.PROFILE_NAME)

    assert active.max_examples == property_policy.MAX_EXAMPLES == 100
    assert active.deadline is not None
    assert active.deadline.total_seconds() == property_policy.DEADLINE.total_seconds()
    assert active.print_blob is True
    assert active.derandomize is False
    assert settings.default.max_examples == active.max_examples


def test_a_failing_property_reports_its_seed_and_the_counterexample(tmp_path: Path) -> None:
    """Issue 41 CA-1: the report carries the seed and the smallest counterexample."""
    report = run_failing_property(tmp_path, seed=None)

    header = re.search(r"property seed: (\d+) \(repeat with CDG_PROPERTY_SEED=\1\)", report)
    summary = re.search(r"CDG_PROPERTY_SEED=(\d+)\s*$", report, re.MULTILINE)
    assert header and summary, report
    assert header.group(1) == summary.group(1)
    assert counterexample(report) == "11"
    assert "reproduce_failure" in report


def test_the_reported_seed_repeats_the_same_failure(tmp_path: Path) -> None:
    """Issue 41 CA-2: given the seed, the failure comes back identical, every time."""
    first = run_failing_property(tmp_path, seed=None)
    seed = re.search(r"property seed: (\d+)", first).group(1)  # type: ignore[union-attr]

    repeats = [run_failing_property(tmp_path, seed=seed) for _ in range(3)]

    assert {counterexample(repeat) for repeat in repeats} == {counterexample(first)}
    assert all(f"property seed: {seed}" in repeat for repeat in repeats)
    failing_blocks = {re.search(r"Failing test case:.*?\n\n", repeat, re.DOTALL).group(0) for repeat in repeats}  # type: ignore[union-attr]
    assert len(failing_blocks) == 1
