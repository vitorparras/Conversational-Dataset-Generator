"""Puts tools/dev and tools/verify-code on the import path, so `check.py` reuses the steps of the
verifier (`steps.reach`, `steps.changes`, `steps.pr_size`) instead of copying them."""

from __future__ import annotations

import sys
from pathlib import Path

DEV = Path(__file__).resolve().parent
ROOT = DEV.parents[1]
VERIFY_CODE = ROOT / "tools" / "verify-code"

for folder in (str(VERIFY_CODE), str(DEV)):
    if folder not in sys.path:
        sys.path.insert(0, folder)
