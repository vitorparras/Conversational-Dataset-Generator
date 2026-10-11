"""Issue 29 CA-4: there is no mode that prints a request body, not even a disabled one."""

import re
from pathlib import Path

import language_service

SOURCE = Path(language_service.__file__).parent
FORBIDDEN = re.compile(r"debug|reload|access_log\s*=\s*True", re.IGNORECASE)


def test_source_names_no_debug_or_reload_mode() -> None:
    hits = [
        f"{path.relative_to(SOURCE)}:{number}: {line.strip()}"
        for path in SOURCE.rglob("*.py")
        for number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), start=1)
        if FORBIDDEN.search(line)
    ]

    assert hits == []
