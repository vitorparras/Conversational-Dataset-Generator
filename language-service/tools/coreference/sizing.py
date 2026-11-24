"""Conversations of a chosen size and the memory of the measuring process."""

from __future__ import annotations

import importlib
import itertools
from collections.abc import Sequence
from dataclasses import dataclass
from pathlib import Path

from coreference.corpus import Message


@dataclass(frozen=True)
class Memory:
    """Resident and peak memory of the process, in bytes."""

    rss: int
    peak: int


@dataclass(frozen=True)
class SizeRow:
    """One conversation size: its messages and characters, the analysis time and the memory."""

    messages: int
    chars: int
    seconds: float
    memory: Memory


def cycled(conversations: Sequence[Sequence[Message]], count: int) -> tuple[Message, ...]:
    """`count` messages taken in order from the corpus, starting over when it ends."""
    pool = [message for messages in conversations for message in messages]
    return tuple(itertools.islice(itertools.cycle(pool), count))


def process_memory() -> Memory:
    """RSS by psutil; the peak is the peak working set on Windows and VmHWM on Linux."""
    info = importlib.import_module("psutil").Process().memory_info()
    return Memory(rss=int(info.rss), peak=int(getattr(info, "peak_wset", 0)) or _linux_peak())


def _linux_peak() -> int:
    for line in Path("/proc/self/status").read_text(encoding="ascii").splitlines():
        if line.startswith("VmHWM:"):
            return int(line.split()[1]) * 1024
    raise ValueError("VmHWM missing from /proc/self/status")
