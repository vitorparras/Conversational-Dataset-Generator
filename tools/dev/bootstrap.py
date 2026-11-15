"""Puts tools/dev and tools/verify-code on the import path, so `check.py` reuses the verifier."""

from __future__ import annotations

import sys
from pathlib import Path

DEV = Path(__file__).resolve().parent
ROOT = DEV.parents[1]
sys.path[:0] = [str(ROOT / "tools" / "verify-code"), str(DEV)]
