"""Coreference models behind the `text -> chains` call of the measurement, imported only when
chosen: none of them is a dependency of the service."""

from __future__ import annotations

import importlib
from typing import Any

from coreference.corpus import Span
from coreference.measurement import Resolver


def stanza(model: str) -> Resolver:
    """Stanza's coreference model at the path `model`, over Stanza's Portuguese tokenizer, whose
    `tokenize` and `mwt` models must be downloaded beforehand: nothing is fetched here."""
    library = importlib.import_module("stanza")
    pipeline = library.Pipeline(
        "pt", processors="tokenize,mwt,coref", coref_model_path=model, download_method=None
    )

    def resolve(text: str) -> tuple[tuple[Span, ...], ...]:
        document = pipeline(text)
        return tuple(_spans(document, chain) for chain in document.coref)

    return resolve


def _spans(document: Any, chain: Any) -> tuple[Span, ...]:
    """Character spans of a chain; a dropped subject has tuple indices and no text: left out."""
    found: list[Span] = []
    for mention in chain.mentions:
        if isinstance(mention.start_word, int):
            sentence = document.sentences[mention.sentence]
            words = sentence.words[mention.start_word : mention.end_word]
            found.append(Span(words[0].parent.start_char, words[-1].parent.end_char))
    return tuple(found)
