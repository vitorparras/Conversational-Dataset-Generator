"""Puts tools/dev and tools/verify-code at the end of the import path (never shadowing `tests`)."""

from __future__ import annotations

import sys
from pathlib import Path

DEV = Path(__file__).resolve().parent
ROOT = DEV.parents[1]
sys.path += [p for p in (str(DEV), str(ROOT / "tools" / "verify-code")) if p not in sys.path]
