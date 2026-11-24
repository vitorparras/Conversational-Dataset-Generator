"""A resolver that answers with chains chosen by the test, over the mentions of the gold corpus."""

from collections.abc import Callable, Sequence

from coreference.corpus import Conversation, Span

FIRST, SECOND, THIRD, FOURTH = "g1", "g2", "g3", "g4"


def chains_by_label(
    conversation: Conversation, labels: Sequence[str | None]
) -> Callable[[str], tuple[tuple[Span, ...], ...]]:
    """One chain per label, holding the mentions given that label; `None` leaves a mention out."""
    grouped: dict[str, list[Span]] = {}
    for mention, label in zip(conversation.mentions, labels, strict=True):
        grouped.setdefault(label or "", []).append(mention.span)
    chains = tuple(tuple(spans) for label, spans in grouped.items() if label)
    return lambda _text: chains
