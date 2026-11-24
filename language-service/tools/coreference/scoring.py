"""Scores predicted chains against the gold mentions of one conversation."""

from __future__ import annotations

import itertools
from collections.abc import Iterable, Iterator, Sequence
from dataclasses import dataclass, fields

from coreference.corpus import Conversation, Mention, Span


@dataclass(frozen=True)
class Ratio:
    """Hits out of a total; with no total there is nothing measured."""

    hits: int
    total: int

    @property
    def value(self) -> float:
        return self.hits / self.total if self.total else 0.0

    def __add__(self, other: Ratio) -> Ratio:
        return Ratio(self.hits + other.hits, self.total + other.total)


@dataclass(frozen=True)
class Scores:
    """Pair precision and recall over the mentions in messages; fusion counts people who share
    a group with someone else; `mixed_` counts only pairs touching a message mixing languages;
    `author_` scores the link from a mention to the name before a message."""

    precision: Ratio
    recall: Ratio
    fusion: Ratio
    mixed_precision: Ratio
    mixed_fusion: Ratio
    author_precision: Ratio
    author_recall: Ratio


@dataclass(frozen=True)
class _Pair:
    first: Mention
    second: Mention
    linked: bool

    @property
    def same(self) -> bool:
        return self.first.entity == self.second.entity


def groups(conversation: Conversation, chains: Sequence[Sequence[Span]]) -> tuple[int, ...]:
    """Group of each gold mention: the chain of the shortest predicted span containing it; with
    none, a group of its own."""
    found: list[int] = []
    for index, mention in enumerate(conversation.mentions):
        containing = [
            (span.end - span.start, chain)
            for chain, spans in enumerate(chains)
            for span in spans
            if span.start <= mention.span.start and mention.span.end <= span.end
        ]
        found.append(min(containing)[1] if containing else len(chains) + index)
    return tuple(found)


def _pairs(conversation: Conversation, group: Sequence[int]) -> Iterator[_Pair]:
    body = [(m, g) for m, g in zip(conversation.mentions, group, strict=True) if not m.author_name]
    for (first, one), (second, other) in itertools.combinations(body, 2):
        yield _Pair(first, second, linked=one == other)


def _precision(pairs: Sequence[_Pair]) -> Ratio:
    linked = [pair for pair in pairs if pair.linked]
    return Ratio(sum(pair.same for pair in linked), len(linked))


def _fusion(pairs: Sequence[_Pair]) -> Ratio:
    people = {m.entity for pair in pairs for m in (pair.first, pair.second)}
    fused = {
        m.entity
        for pair in pairs
        if pair.linked and not pair.same
        for m in (pair.first, pair.second)
    }
    return Ratio(len(fused), len(people))


def _author_link(conversation: Conversation, group: Sequence[int]) -> tuple[Ratio, Ratio]:
    named: dict[int, set[str]] = {}
    for mention, g in zip(conversation.mentions, group, strict=True):
        if mention.author_name:
            named.setdefault(g, set()).add(mention.entity)
    body = [(m, named.get(g, set())) for m, g in zip(conversation.mentions, group, strict=True)]
    body = [(m, authors) for m, authors in body if not m.author_name]
    linked = [(m, authors) for m, authors in body if authors]
    mine = [(m, authors) for m, authors in body if m.entity in conversation.authors]
    return (
        Ratio(sum(authors == {m.entity} for m, authors in linked), len(linked)),
        Ratio(sum(m.entity in authors for m, authors in mine), len(mine)),
    )


def score(conversation: Conversation, chains: Sequence[Sequence[Span]]) -> Scores:
    """The scores of one conversation."""
    group = groups(conversation, chains)
    pairs = list(_pairs(conversation, group))
    mixed = [pair for pair in pairs if pair.first.mixed or pair.second.mixed]
    same = [pair for pair in pairs if pair.same]
    author_precision, author_recall = _author_link(conversation, group)
    return Scores(
        precision=_precision(pairs),
        recall=Ratio(sum(pair.linked for pair in same), len(same)),
        fusion=_fusion(pairs),
        mixed_precision=_precision(mixed),
        mixed_fusion=_fusion(mixed),
        author_precision=author_precision,
        author_recall=author_recall,
    )


def total(scores: Iterable[Scores]) -> Scores:
    """The scores of many conversations: hits and totals add up."""
    names = [field.name for field in fields(Scores)]
    result = Scores(*(Ratio(0, 0) for _ in names))
    for one in scores:
        result = Scores(*(getattr(result, name) + getattr(one, name) for name in names))
    return result
