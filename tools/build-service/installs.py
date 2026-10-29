"""Takes out of the installed packages what only says where the build ran, and works nowhere else.

`uv pip install` writes two kinds of file that carry the path of the machine that built the folder:
the launcher of each console script (a `.exe` with the interpreter's path, or a shebang) and, for a
package installed from a folder, `direct_url.json` and `uv_cache.json`. The service starts by
`-m language_service`, so no launcher is used; and a launcher that points at the build path stops
working the moment the folder moves, which is the whole point of the folder. Taking them out, with
their lines in RECORD, makes two builds of one commit the same bytes and leaves nothing dangling.
Only the packages the build installed are touched: the ones the interpreter brings stay as they are.
"""

from __future__ import annotations

import csv
import io
import json
import os
from pathlib import Path

RECORD = "RECORD"
DIRECT_URL = "direct_url.json"
LOCAL_ONLY = (DIRECT_URL, "uv_cache.json")
LOCAL_URL = "file:"


def installed(site_packages: Path) -> frozenset[str]:
    """The names of the packages whose metadata ``site_packages`` holds."""
    return frozenset(path.name for path in site_packages.glob("*.dist-info"))


def normal(path: Path) -> str:
    """The path normalized to compare as written: no `..`, one separator, one case."""
    return os.path.normcase(os.path.normpath(path))


def inside(path: Path, directory: Path) -> bool:
    """Whether ``path`` is ``directory`` or lies under it, compared as written, not resolved."""
    return Path(normal(path)).is_relative_to(normal(directory))


def is_local(metadata: Path) -> bool:
    """Whether the package was installed from a folder of this machine, by its direct URL."""
    direct = metadata / DIRECT_URL
    if not direct.is_file():
        return False
    return str(json.loads(direct.read_text(encoding="utf-8")).get("url", "")).startswith(LOCAL_URL)


def scrub_one(metadata: Path, site_packages: Path, scripts: Path) -> int:
    """Deletes the launchers and local-only files of one package and their lines in its RECORD.

    Returns how many files went away.
    """
    local = is_local(metadata)
    local_only = {normal(metadata / name) for name in LOCAL_ONLY if local}
    kept: list[list[str]] = []
    removed = 0
    record = metadata / RECORD
    for row in csv.reader(io.StringIO(record.read_text(encoding="utf-8"))):
        if not row:
            continue
        target = Path(os.path.normpath(site_packages / row[0].replace("\\", "/")))
        if inside(target, scripts) or normal(target) in local_only:
            target.unlink(missing_ok=True)
            removed += 1
        else:
            kept.append(row)
    for name in LOCAL_ONLY if local else ():  # uv_cache.json is in no RECORD
        (metadata / name).unlink(missing_ok=True)
    out = io.StringIO()
    csv.writer(out, lineterminator="\n").writerows(kept)
    record.write_bytes(out.getvalue().encode("utf-8"))
    return removed


def scrub(site_packages: Path, scripts: Path, before: frozenset[str]) -> int:
    """Scrubs every package that was not in ``before``; returns how many files went away."""
    added = (path for path in sorted(site_packages.glob("*.dist-info")) if path.name not in before)
    return sum(scrub_one(path, site_packages, scripts) for path in added)
