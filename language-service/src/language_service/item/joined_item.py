"""An item joined into one text, one line per message with its author, and its mentions placed back.

The join is the one the coreference measurement reads (``"{author}: {text}"`` and a line break), so
the model sees the item as it was measured. A mention is kept only when it falls inside one text or
inside one author name: over a text it is an occurrence of that text; over a name it gives that
author its group and is never an occurrence; across the boundaries it is dropped.
"""

from collections.abc import Iterable, Sequence
from dataclasses import dataclass

from language_service.contract import AnalyzeItemResponse, AuthorGroup, GroupedOccurrence, ItemText
from language_service.recognizers.mention import Mention
from language_service.recognizers.occurrence import Occurrence

AUTHOR_SEPARATOR = ": "
LINE_END = "\n"


@dataclass(frozen=True)
class _Line:
    """Where one message lies in the joined text: its author name, if any, and then its text."""

    author: str | None
    author_start: int
    text_start: int
    text_end: int


@dataclass(frozen=True)
class JoinedItem:
    """The joined text the recognizer reads, and where each message of the item lies in it."""

    text: str
    lines: tuple[_Line, ...]


def join_item(texts: Sequence[ItemText]) -> JoinedItem:
    """One line per text, in the order given, with its author ahead of it when it has one."""
    parts: list[str] = []
    lines: list[_Line] = []
    cursor = 0
    for item_text in texts:
        prefix = "" if item_text.author is None else f"{item_text.author}{AUTHOR_SEPARATOR}"
        text_start = cursor + len(prefix)
        lines.append(_Line(item_text.author, cursor, text_start, text_start + len(item_text.text)))
        parts.append(f"{prefix}{item_text.text}{LINE_END}")
        cursor = text_start + len(item_text.text) + len(LINE_END)
    return JoinedItem("".join(parts), tuple(lines))


def place_mentions(
    item: JoinedItem, mentions: Sequence[Mention], model_version: str | None
) -> AnalyzeItemResponse:
    """Each mention in its text with its group, and one group for each distinct author."""
    placed = (_occurrence(item, mention) for mention in mentions)
    return AnalyzeItemResponse(
        occurrences=tuple(occurrence for occurrence in placed if occurrence is not None),
        author_groups=_author_groups(item, mentions),
        model_version=model_version,
    )


def _occurrence(item: JoinedItem, mention: Mention) -> GroupedOccurrence | None:
    span = mention.occurrence
    found = next(
        (
            (index, line)
            for index, line in enumerate(item.lines)
            if line.text_start <= span.start and span.end <= line.text_end
        ),
        None,
    )
    if found is None:
        return None
    index, line = found
    return GroupedOccurrence(
        text_index=index,
        start=span.start - line.text_start,
        end=span.end - line.text_start,
        kind=span.kind,
        part_of_speech=span.part_of_speech,
        syntactic_role=span.syntactic_role,
        group=mention.group,
    )


def _author_groups(item: JoinedItem, mentions: Sequence[Mention]) -> tuple[AuthorGroup, ...]:
    """The group of the first mention over each name; a name nothing mentions gets an unused one."""
    named: dict[str, int] = {}
    for mention in mentions:
        author = _author_under(item.lines, mention.occurrence)
        if author is not None:
            named.setdefault(author, mention.group)
    unused = max((mention.group for mention in mentions), default=-1) + 1
    groups: list[AuthorGroup] = []
    for author in dict.fromkeys(line.author for line in item.lines if line.author is not None):
        if author not in named:
            named[author] = unused
            unused += 1
        groups.append(AuthorGroup(author=author, group=named[author]))
    return tuple(groups)


def _author_under(lines: Iterable[_Line], span: Occurrence) -> str | None:
    return next(
        (
            line.author
            for line in lines
            if line.author is not None
            and line.author_start <= span.start
            and span.end <= line.author_start + len(line.author)
        ),
        None,
    )
