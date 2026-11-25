"""Each text of an item is read alone, and the name rule of decision 0060 groups the whole item."""

import json
import logging
from http import HTTPStatus

from fastapi.testclient import TestClient

from language_service.contract import AnalyzeItemRequest, AnalyzeItemResponse, ItemText
from language_service.logging_ import RequestLog
from language_service.routes import Routes
from support.contract_fixtures import fixture_text
from support.recognizers import Word, WordRecognizer
from support.synthetic import SECRET_HEADER

AUTHOR_A = "ZQX-AUTOR-A"
AUTHOR_B = "ZQX-AUTOR-B"


def analyse(
    recognizer: WordRecognizer, *texts: ItemText, language: str = "pt"
) -> AnalyzeItemResponse:
    routes = Routes(recognizer, RequestLog(logging.getLogger("language_service")))
    request = AnalyzeItemRequest(
        texts=list(texts), language=language, reference="ref-0908", contract_version="2"
    )
    return routes.analyze_item(request)


def said(text: str, author: str | None = None) -> ItemText:
    return ItemText(text=text, author=author)


def groups(response: AnalyzeItemResponse) -> list[int]:
    return [occurrence.group for occurrence in response.occurrences]


def author_group(response: AnalyzeItemResponse, author: str) -> int:
    return next(group.group for group in response.author_groups if group.author == author)


def test_each_text_is_recognized_alone_without_its_author() -> None:
    recognizer = WordRecognizer((Word("Ana Souza"),))

    analyse(recognizer, said("oi", "Bruno Lima"), said("solto"), language="es")

    assert recognizer.asked == [("oi", "es"), ("solto", "es")]


def test_every_occurrence_comes_back_in_its_text_with_its_group() -> None:
    recognizer = WordRecognizer((Word("Ana Souza"), Word("ÁNA SOUZA"), Word("Bruno Lima")))

    response = analyse(
        recognizer,
        said("\U0001f642 Ana Souza vem", AUTHOR_A),
        said("ÁNA SOUZA confirmou com Bruno Lima", AUTHOR_B),
    )

    places = [(o.text_index, o.start, o.end) for o in response.occurrences]
    assert places == [(0, 2, 11), (1, 0, 9), (1, 24, 34)]
    first, again, other = groups(response)
    assert (first == again, first == other) == (True, False)


def test_author_names_come_back_with_their_group_and_never_as_occurrences() -> None:
    recognizer = WordRecognizer((Word("ana souza"),))

    response = analyse(
        recognizer,
        said("oi", "Ana Souza"),
        said("falei com ana souza", AUTHOR_B),
        said("de novo", "Ana Souza"),
    )

    assert [(o.text_index, o.start, o.end) for o in response.occurrences] == [(1, 10, 19)]
    assert [group.author for group in response.author_groups] == ["Ana Souza", AUTHOR_B]
    assert groups(response) == [author_group(response, "Ana Souza")]
    assert author_group(response, AUTHOR_B) != author_group(response, "Ana Souza")


def test_one_word_mention_joins_the_only_longer_name_holding_it() -> None:
    recognizer = WordRecognizer((Word("Ana"), Word("Bruno Lima")))

    response = analyse(
        recognizer, said("a Ana chegou", "Ana Souza"), said("Bruno Lima viu", AUTHOR_B)
    )

    ana, bruno = groups(response)
    assert ana == author_group(response, "Ana Souza")
    assert bruno != ana


def test_one_word_mention_joins_a_longer_mention_holding_it() -> None:
    recognizer = WordRecognizer((Word("souza"), Word("Ana Souza")))

    response = analyse(recognizer, said("Ana Souza veio"), said("souza saiu"))

    name, part = groups(response)
    assert part == name


def test_one_word_mention_with_two_candidate_names_keeps_its_own_group() -> None:
    recognizer = WordRecognizer((Word("Ana"), Word("Ana Lima")))

    response = analyse(recognizer, said("Ana Lima e Ana", "Ana Souza"))

    lima, ana = groups(response)
    assert ana not in (lima, author_group(response, "Ana Souza"))


def test_one_word_mention_with_a_pair_of_its_text_keeps_the_pair_apart_from_the_name() -> None:
    recognizer = WordRecognizer((Word("Ana"), Word("ana")))

    response = analyse(recognizer, said("Ana veio", "Ana Souza"), said("ana saiu"))

    first, second = groups(response)
    assert (first == second, first == author_group(response, "Ana Souza")) == (True, False)


def test_one_word_mention_with_an_author_of_its_text_takes_the_author_group() -> None:
    recognizer = WordRecognizer((Word("Ana"),))

    response = analyse(recognizer, said("Ana veio", "Ana"), said("oi", "Ana Souza"))

    assert groups(response) == [author_group(response, "Ana")]
    assert author_group(response, "Ana") != author_group(response, "Ana Souza")


def test_a_word_inside_a_longer_word_is_no_candidate() -> None:
    recognizer = WordRecognizer((Word("Ana"),))

    response = analyse(recognizer, said("Ana veio", "Anabela Souza"))

    assert groups(response) != [author_group(response, "Anabela Souza")]


def test_each_pronoun_has_a_group_of_its_own() -> None:
    recognizer = WordRecognizer((Word("ela", part_of_speech="PRON"),))

    response = analyse(recognizer, said("ela viu ela", "Ela Souza"), said("ela"))

    pronouns = groups(response)
    assert len({*pronouns, author_group(response, "Ela Souza")}) == len(pronouns) + 1


def test_places_group_by_their_text_and_never_with_a_person() -> None:
    words = (
        Word("São Paulo", kind="LOCATION"),
        Word("sao paulo", kind="LOCATION"),
        Word("Paulo Lima"),
    )

    response = analyse(
        WordRecognizer(words), said("São Paulo", "Paulo"), said("sao paulo e Paulo Lima")
    )

    place, again, person = groups(response)
    assert (place == again, place == person, place == author_group(response, "Paulo")) == (
        True,
        False,
        False,
    )


def test_one_word_author_never_joins_a_longer_name() -> None:
    response = analyse(WordRecognizer((Word("Paulo Lima"),)), said("Paulo Lima veio", "Paulo"))

    assert groups(response) != [author_group(response, "Paulo")]


def test_no_group_mixes_kinds_of_one_name() -> None:
    recognizer = WordRecognizer((Word("Lima", kind="ORGANIZATION"), Word("LIMA")))

    response = analyse(recognizer, said("Lima contratou LIMA", "lima"))

    organization, person = groups(response)
    assert (person == author_group(response, "lima"), organization == person) == (True, False)


def test_a_longer_place_is_no_candidate_of_a_person() -> None:
    recognizer = WordRecognizer((Word("Lima"), Word("Rua Lima", kind="LOCATION")))

    response = analyse(recognizer, said("Lima mora na Rua Lima"))

    person, place = groups(response)
    assert person != place


def test_one_word_place_never_joins_a_longer_name() -> None:
    recognizer = WordRecognizer((Word("Lima", kind="ORGANIZATION"), Word("Ana Lima")))

    response = analyse(recognizer, said("Ana Lima e Lima"))

    name, organization = groups(response)
    assert organization != name


def test_each_occurrence_keeps_the_kind_and_analysis_the_recognizer_gave() -> None:
    response = analyse(
        WordRecognizer((Word("PESSOA_1", "LOCATION", "NOUN", "obl"),)),
        said("PESSOA_1 vem"),
    )

    assert [(o.kind, o.part_of_speech, o.syntactic_role) for o in response.occurrences] == [
        ("LOCATION", "NOUN", "obl")
    ]


def test_text_without_author_has_its_occurrences_and_no_group_of_author() -> None:
    response = analyse(WordRecognizer((Word("PESSOA_1"),)), said("PESSOA_1 chegou"))

    places = [(o.text_index, o.start, o.end) for o in response.occurrences]
    assert (places, response.author_groups) == ([(0, 0, 8)], ())


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

