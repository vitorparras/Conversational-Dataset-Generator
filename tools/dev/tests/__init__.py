"""Makes the modules of tools/dev and the steps of the verifier importable by the tests (appended,
so that they never shadow the `tests` package of tools/)."""

import sys
from pathlib import Path

DEV = Path(__file__).resolve().parents[1]
for folder in (DEV, DEV.parent / "verify-code"):
    if str(folder) not in sys.path:
        sys.path.append(str(folder))
