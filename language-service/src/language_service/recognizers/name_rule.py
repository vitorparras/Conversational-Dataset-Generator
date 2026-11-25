"""The groups of an item by the name rule of decision 0060, over every text and author name.

Names are compared without case and accents. Mentions of one kind with the same name share a group,
and a person mention shares it with the author of that name. A one-word person mention with no pair
of its name joins the only longer person name, of a mention or an author, holding that word; with
two or more candidates it keeps a group of its own. Each pronoun has a group of its own, and no
group mixes kinds.
"""

import unicodedata
from collections import Counter
from collections.abc import Sequence
from dataclasses import dataclass

from language_service.contract import AnalyzeItemResponse, AuthorGroup, GroupedOccurrence, ItemText
from language_service.recognizers.occurrence import Occurrence

PERSON = "PERSON"
PRONOUN = "PRON"


@dataclass(frozen=True)
class _Name:
    """What the rule compares: the kind and the name folded; a pronoun also has its own place."""

    kind: str
    folded: str
    place: tuple[int, int] | None = None


@dataclass(frozen=True)
class _Found:
    """One occurrence, the index of its text and the name the rule reads in it."""

    text_index: int
    occurrence: Occurrence
    name: _Name


def group_by_name(
    texts: Sequence[ItemText], found: Sequence[Sequence[Occurrence]], model_version: str | None
) -> AnalyzeItemResponse:
    """Each occurrence of ``found``, given text by text, in its text with the group of its name."""
    mentions = [
        _Found(index, occurrence, _name_of(texts[index].text, index, occurrence))
        for index, occurrences in enumerate(found)
        for occurrence in occurrences
    ]
    authors = list(dict.fromkeys(text.author for text in texts if text.author is not None))
    joined = _joined_names([mention.name for mention in mentions], [_person(a) for a in authors])
    numbers: dict[_Name, int] = {}

    def group(name: _Name) -> int:
        return numbers.setdefault(joined.get(name, name), len(numbers))

    occurrences = tuple(_grouped(mention, group(mention.name)) for mention in mentions)
    return AnalyzeItemResponse(
        occurrences=occurrences,
        author_groups=tuple(AuthorGroup(author=a, group=group(_person(a))) for a in authors),
        model_version=model_version,
    )


def _joined_names(mentioned: Sequence[_Name], authors: Sequence[_Name]) -> dict[_Name, _Name]:
    """The one-word person names with no pair that join the only longer person name holding them."""
    seen = Counter([*mentioned, *authors])
    longer = {name for name in seen if name.kind == PERSON and len(name.folded.split()) > 1}
    joined: dict[_Name, _Name] = {}
    for name in dict.fromkeys(mentioned):
        if name.kind != PERSON or name.place is not None or seen[name] > 1 or " " in name.folded:
            continue
        candidates = [other for other in longer if name.folded in other.folded.split()]
        if len(candidates) == 1:
            joined[name] = candidates[0]
    return joined


def _name_of(text: str, text_index: int, occurrence: Occurrence) -> _Name:
    folded = _fold(text[occurrence.start : occurrence.end])
    if occurrence.part_of_speech == PRONOUN:
        return _Name(occurrence.kind, folded, (text_index, occurrence.start))
    return _Name(occurrence.kind, folded)


def _person(author: str) -> _Name:
    return _Name(PERSON, _fold(author))


def _fold(name: str) -> str:
    """The name without case, accents nor repeated spaces, as the rule compares it."""
    decomposed = unicodedata.normalize("NFD", name.casefold())
    return " ".join("".join(c for c in decomposed if not unicodedata.combining(c)).split())


def _grouped(mention: _Found, group: int) -> GroupedOccurrence:
    occurrence = mention.occurrence
    return GroupedOccurrence(
        text_index=mention.text_index,
        start=occurrence.start,
        end=occurrence.end,
        kind=occurrence.kind,
        part_of_speech=occurrence.part_of_speech,
        syntactic_role=occurrence.syntactic_role,
        group=group,
    )
