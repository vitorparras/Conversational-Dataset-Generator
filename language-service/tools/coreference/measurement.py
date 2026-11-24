"""Runs a resolver over the gold corpus and over growing conversations, and judges the result."""

from __future__ import annotations

from collections.abc import Callable, Sequence
from dataclasses import dataclass

from coreference.corpus import Message, Span, join
from coreference.scoring import Ratio, Scores, score, total
from coreference.sizing import Memory, SizeRow, cycled

type Resolver = Callable[[str], Sequence[Sequence[Span]]]

SLICES = {
    "precision": "precisão",
    "mixed_precision": "precisão nas mensagens que misturam línguas",
}


@dataclass(frozen=True)
class Probe:
    """Where time and memory are read; the tests give fixed readings."""

    clock: Callable[[], float]
    memory: Callable[[], Memory]


@dataclass(frozen=True)
class Report:
    """The scores over the corpus, the memory with the model loaded and one row per size."""

    scores: Scores
    loaded: Memory
    sizes: tuple[SizeRow, ...]


def measure(
    conversations: Sequence[Sequence[Message]],
    resolver: Resolver,
    sizes: Sequence[int],
    probe: Probe,
) -> Report:
    """Scores every conversation of the corpus, then times each size in increasing order."""
    loaded = probe.memory()
    joined = (join(messages) for messages in conversations)
    scores = total(score(conversation, resolver(conversation.text)) for conversation in joined)
    rows = tuple(_size_row(conversations, count, resolver, probe) for count in sorted(sizes))
    return Report(scores, loaded, rows)


def _size_row(
    conversations: Sequence[Sequence[Message]], count: int, resolver: Resolver, probe: Probe
) -> SizeRow:
    text = join(cycled(conversations, count)).text
    started = probe.clock()
    resolver(text)
    seconds = probe.clock() - started
    return SizeRow(count, len(text), seconds, probe.memory())


def shortfalls(scores: Scores, threshold: float) -> tuple[str, ...]:
    """Why the model fails, one reason per slice below the threshold or with nothing linked."""
    reasons = (
        _shortfall(label, getattr(scores, name), threshold) for name, label in SLICES.items()
    )
    return tuple(reason for reason in reasons if reason)


def decimal(value: float) -> str:
    """A ratio as the operator reads it: three places and a decimal comma."""
    return f"{value:.3f}".replace(".", ",")


def _shortfall(label: str, ratio: Ratio, threshold: float) -> str:
    if not ratio.total:
        return f"{label}: nenhum par ligado a medir"
    if ratio.value < threshold:
        return f"{label}: {decimal(ratio.value)} abaixo de {decimal(threshold)}"
    return ""
