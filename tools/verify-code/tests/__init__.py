"""Makes the `steps` package importable by the tests, whichever directory discovery starts from."""

import sys
from pathlib import Path

VERIFY_CODE = str(Path(__file__).resolve().parents[1])
if VERIFY_CODE not in sys.path:
    sys.path.insert(0, VERIFY_CODE)
