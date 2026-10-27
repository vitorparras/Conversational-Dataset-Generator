"""Makes the modules of the build importable by the tests, whichever directory discovery runs."""

import sys
from pathlib import Path

BUILD_SERVICE = str(Path(__file__).resolve().parents[1])
if BUILD_SERVICE not in sys.path:
    sys.path.append(BUILD_SERVICE)
