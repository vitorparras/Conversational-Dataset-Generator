"""A deliberately wrong service, for the negative proof: it copies the text where LEAK_TO says.

It is the real entry point with one change: the recognizer writes the text it receives to one
destination. The privacy sweep must fail on it and name that destination.
"""

import json
import os
import sys
import tempfile
from collections.abc import Sequence
from pathlib import Path

from language_service import __main__ as entry
from language_service.contract import Occurrence
from language_service.recognizers.empty import EmptyRecognizer


class LeakyRecognizer(EmptyRecognizer):
    """Finds nothing, and writes the text to the destination named by LEAK_TO."""

    def recognize(self, text: str, language: str) -> Sequence[Occurrence]:
        target = os.environ["LEAK_TO"]
        if target == "stdout":
            sys.stdout.write(text + "\n")
            sys.stdout.flush()
        elif target == "stderr":
            sys.stderr.write(text + "\n")
            sys.stderr.flush()
        elif target == "log-file":
            path = sys.argv[sys.argv.index("--log-file") + 1]
            with open(path, "a", encoding="utf-8") as handle:
                handle.write(text + "\n")
        elif target == "temp-dir":
            Path(tempfile.gettempdir(), "scratch.txt").write_text(text, encoding="utf-8")
        elif target == "work-dir":
            Path("scratch.txt").write_text(text, encoding="utf-8")
        elif target == "escaped-fragment":
            # Only the non-ASCII part, JSON-escaped: no plain marker for a naive search to find.
            sys.stdout.write(json.dumps("ação 😀") + "\n")
            sys.stdout.flush()
        return ()


entry.EmptyRecognizer = LeakyRecognizer  # type: ignore[misc,assignment]

if __name__ == "__main__":
    raise SystemExit(entry.main())
