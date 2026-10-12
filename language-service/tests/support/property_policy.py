"""How every property of this project is run (decision 0007, issue 41).

A pytest plugin, loaded by ``conftest.py``. It does three things:

* registers and loads the one hypothesis profile, so the number of examples, the deadline and the
  failure blob are declared in a single place and not lowered per test to fit the time;
* gives every property a seed: ``CDG_PROPERTY_SEED`` when set, a fresh one otherwise;
* prints that seed in the header of the run and again in the summary of a failing run, so a failure
  seen in a pull request can be repeated: ``CDG_PROPERTY_SEED=<seed> pytest ...``.
"""

import os
import secrets
from datetime import timedelta

import pytest
from hypothesis import seed as hypothesis_seed
from hypothesis import settings

#: Environment variable that repeats a run from the seed a failure reported.
SEED_VARIABLE = "CDG_PROPERTY_SEED"

#: The profile every property runs under. Declared, not inherited from a library default.
PROFILE_NAME = "foundation"
MAX_EXAMPLES = 100
DEADLINE = timedelta(milliseconds=2000)

settings.register_profile(
    PROFILE_NAME,
    max_examples=MAX_EXAMPLES,
    deadline=DEADLINE,
    print_blob=True,
    derandomize=False,
    database=None,
)
settings.load_profile(PROFILE_NAME)

#: The seed of this run, fixed at import so the header and the properties agree.
RUN_SEED = int(os.environ.get(SEED_VARIABLE) or secrets.randbits(32))


def pytest_report_header() -> str:
    """The seed of the run, in the first lines of the output."""
    return f"property seed: {RUN_SEED} (repeat with {SEED_VARIABLE}={RUN_SEED})"


def pytest_collection_modifyitems(config: pytest.Config, items: list[pytest.Item]) -> None:
    """Apply the seed of the run to every property that has none of its own."""
    if config.getoption("hypothesis_seed", default=None) is not None:
        return
    for item in items:
        test = getattr(item, "obj", None)
        if getattr(test, "is_hypothesis_test", False) and getattr(
            test, "_hypothesis_internal_use_seed", None
        ) is None:
            hypothesis_seed(RUN_SEED)(test)


def pytest_terminal_summary(terminalreporter: pytest.TerminalReporter) -> None:
    """Repeat the seed where a person reading a failure will look."""
    if terminalreporter.stats.get("failed"):
        terminalreporter.write_sep("=", "property seed")
        terminalreporter.write_line(f"{SEED_VARIABLE}={RUN_SEED}")
