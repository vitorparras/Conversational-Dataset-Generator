"""Synthetic material for the tests: an improbable marker, never a plausible name or real secret."""

from typing import Final

MARKER: Final = "ZQX-MARKER-7731"
SECRET: Final = b"ZQX-MARKER-7731-startup-secret"
SECRET_HEADER: Final = {"x-service-secret": SECRET.decode("ascii")}
MARKED_BODY: Final = {"text": MARKER}
ANALYZE_BODY: Final = {"text": MARKER, "language": "pt", "reference": "r1", "contract_version": "1"}
