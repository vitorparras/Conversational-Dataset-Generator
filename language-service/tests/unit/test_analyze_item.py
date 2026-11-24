"""A whole item is read as one text and each occurrence comes back in its text with its group."""

import json
from http import HTTPStatus

from fastapi.testclient import TestClient

from language_service.contract import AnalyzeItemRequest, AuthorGroup, GroupedOccurrence, ItemText
from language_service.recognizers.joined_item import join_item, place_mentions
from language_service.recognizers.empty import EmptyRecognizer
from language_service.recognizers.mention import Mention
from language_service.recognizers.occurrence import Occurrence
from support.contract_fixtures import fixture_text
from support.recognizers import ResolvingRecognizer
from support.synthetic import SECRET_HEADER

AUTHOR_A = "ZQX-AUTOR-A"
AUTHOR_B = "ZQX-AUTOR-B"


def analyse(recognizer: ResolvingRecognizer, *texts: ItemText) -> tuple[object, object]:
    item = join_item(texts)
    response = place_mentions(item, recognizer.resolve(item.text, "pt"), "zqx-model-2")
    return response.occurrences, response.author_groups


def found(text_index: int, start: int, end: int, group: int) -> GroupedOccurrence:
    return GroupedOccurrence(
        text_index=text_index,
        start=start,
        end=end,
        kind="PERSON",
        part_of_speech="PROPN",
        syntactic_role="nsubj",
        group=group,
    )


def test_texts_are_joined_one_line_each_with_the_author_ahead() -> None:
    item = join_item((ItemText(text="oi", author=AUTHOR_A), ItemText(text="solto", author=None)))

    assert item.text == f"{AUTHOR_A}: oi\nsolto\n"


def test_every_occurrence_comes_back_in_its_text_with_its_group() -> None:
    recognizer = ResolvingRecognizer((("PESSOA_1", 0), ("ela", 0), ("PESSOA_2", 1)))

    occurrences, groups = analyse(
        recognizer,
        ItemText(text="\U0001f642 PESSOA_1 vem amanhã", author=AUTHOR_A),
        ItemText(text="ela confirmou com PESSOA_2", author=AUTHOR_B),
    )

    assert occurrences == (found(0, 2, 10, 0), found(1, 0, 3, 0), found(1, 18, 26, 1))
    assert groups == (AuthorGroup(author=AUTHOR_A, group=2), AuthorGroup(author=AUTHOR_B, group=3))


def test_author_names_come_back_with_their_group_and_never_as_occurrences() -> None:
    recognizer = ResolvingRecognizer(((AUTHOR_A, 4),))

    occurrences, groups = analyse(
        recognizer,
        ItemText(text="oi", author=AUTHOR_A),
        ItemText(text=f"falei com {AUTHOR_A}", author=AUTHOR_B),
        ItemText(text="de novo", author=AUTHOR_A),
    )

    assert occurrences == (found(1, 10, 21, 4),)
    assert groups == (AuthorGroup(author=AUTHOR_A, group=4), AuthorGroup(author=AUTHOR_B, group=5))


def test_each_occurrence_keeps_the_kind_and_analysis_the_recognizer_gave() -> None:
    item = join_item((ItemText(text="PESSOA_1 vem", author=None),))
    mention = Mention(Occurrence(0, 8, "LOC", "NOUN", "obl"), 0)

    response = place_mentions(item, (mention,), None)

    assert [(o.kind, o.part_of_speech, o.syntactic_role) for o in response.occurrences] == [
        ("LOC", "NOUN", "obl")
    ]


def test_first_mention_over_a_name_gives_the_author_its_group() -> None:
    item = join_item((ItemText(text="oi", author=AUTHOR_A), ItemText(text="oi", author=AUTHOR_A)))
    first, second = (
        Occurrence(start, start + len(AUTHOR_A), "PERSON", "PROPN", "nsubj") for start in (0, 16)
    )

    response = place_mentions(item, (Mention(first, 7), Mention(second, 2)), None)

    assert response.author_groups == (AuthorGroup(author=AUTHOR_A, group=7),)


def test_mention_across_two_texts_is_dropped() -> None:
    recognizer = ResolvingRecognizer((("amanhã\nZQX", 3),))

    occurrences, groups = analyse(
        recognizer,
        ItemText(text="vem amanhã", author=AUTHOR_A),
        ItemText(text="ok", author=AUTHOR_B),
    )

    assert occurrences == ()
    assert groups == (AuthorGroup(author=AUTHOR_A, group=4), AuthorGroup(author=AUTHOR_B, group=5))


def test_text_without_author_has_its_occurrences_and_no_group_of_author() -> None:
    occurrences, groups = analyse(
        ResolvingRecognizer((("PESSOA_1", 0),)), ItemText(text="PESSOA_1 chegou", author=None)
    )

    assert (occurrences, groups) == ((found(0, 0, 8, 0),), ())


def test_empty_recognizer_gives_one_group_per_author_and_no_occurrence(
    client: TestClient,
) -> None:
    body = json.loads(fixture_text("accepted/item-request.json"))

    response = client.post("/analyze-item", json=body, headers=SECRET_HEADER)

    assert response.json() == {
        "occurrences": [],
        "author_groups": [{"author": AUTHOR_A, "group": 0}, {"author": AUTHOR_B, "group": 1}],
        "model_version": None,
    }


def test_item_without_its_texts_is_refused_without_echo(client: TestClient) -> None:
    body = {"language": "pt", "reference": "ref-0908", "contract_version": "2"}

    response = client.post("/analyze-item", json=body, headers=SECRET_HEADER)

    assert (response.status_code, response.json()) == (
        HTTPStatus.BAD_REQUEST,
        {"error": "invalid_request"},
    )


def test_empty_recognizer_resolves_nothing() -> None:
    request = AnalyzeItemRequest.model_validate_json(fixture_text("accepted/item-request.json"))

    assert EmptyRecognizer().resolve(join_item(request.texts).text, "pt") == ()
