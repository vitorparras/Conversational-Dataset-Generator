"""The sweep of issue 33: everything a service run wrote, searched for the seeded material."""

import json
from collections.abc import Iterable, Sequence
from dataclasses import dataclass

from support.server import RunningService

MARKER = "ZQX-MARKER-7731"
# Not ASCII on purpose: a log that escapes the text (ç) still leaks it, and the sweep must see that.
SEEDED_TEXT = f"alfa {MARKER} ação 😀 omega"
SEEDS = (SEEDED_TEXT, MARKER, "alfa", "omega", "ação 😀")


@dataclass(frozen=True)
class Destination:
    """One place the service may have written to, and what it holds."""

    name: str
    content: bytes


def variants(seed: str) -> set[bytes]:
    """The ways a seed can show up in written bytes: raw, JSON-escaped, JSON-literal, repr."""
    forms = {
        seed,
        json.dumps(seed)[1:-1],
        json.dumps(seed, ensure_ascii=False)[1:-1],
        repr(seed)[1:-1],
        repr(seed.encode("utf-8"))[2:-1],
    }
    return {form.encode("utf-8") for form in forms}


def find_leaks(destinations: Iterable[Destination], seeds: Sequence[str] = SEEDS) -> list[str]:
    """Name every destination holding any form of any seed; an empty list is a clean sweep."""
    leaks: list[str] = []
    for destination in destinations:
        for seed in seeds:
            if any(form in destination.content for form in variants(seed)):
                leaks.append(f"{destination.name} holds material from the seed {seed[:4]!r}...")
                break
    return leaks


def collect(service: RunningService) -> list[Destination]:
    """Everything the stopped service wrote: its two streams, its log file and every file it left."""
    destinations = [
        Destination("stdout", service.stdout_text.encode("utf-8")),
        Destination("stderr", service.stderr_text.encode("utf-8")),
    ]
    if service.log_file.exists():
        destinations.append(Destination("log-file", service.log_file.read_bytes()))
    for label, root in (("temp-dir", service.temp_dir), ("work-dir", service.work_dir)):
        for path in sorted(root.rglob("*")):
            if path.is_file():
                destinations.append(Destination(f"{label}:{path.relative_to(root).as_posix()}", path.read_bytes()))
    return destinations


def files_left_behind(service: RunningService) -> list[str]:
    """Every file under the process's temporary and working directories."""
    return [
        f"{label}:{path.relative_to(root).as_posix()}"
        for label, root in (("temp-dir", service.temp_dir), ("work-dir", service.work_dir))
        for path in sorted(root.rglob("*"))
        if path.is_file()
    ]
