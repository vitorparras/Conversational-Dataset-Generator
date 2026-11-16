"""Puts tools/dev and tools/verify-code on the import path, so `check.py` reuses the verifier.
They go at the end, so that they never shadow the `tests` package of tools/."""

from __future__ import annotations

import sys
from pathlib import Path

DEV = Path(__file__).resolve().parent
ROOT = DEV.parents[1]
for folder in (DEV, ROOT / "tools" / "verify-code"):
    if str(folder) not in sys.path:
        sys.path.append(str(folder))
