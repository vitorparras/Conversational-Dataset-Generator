"""The coreference measurement: what it reports and when it fails."""

import itertools
import json
import re
from pathlib import Path

import measure_coreference
import pytest
from coreference.corpus import Message, Span, join, load
from coreference.measurement import Probe, Resolver, measure, shortfalls
from coreference.scoring import Ratio
from coreference.sizing import Memory, SizeRow

from support.coreference_chains import FIRST, FOURTH, SECOND, THIRD, chains_by_label

CORPUS = measure_coreference.CORPUS
NO_SIZES: tuple[int, ...] = ()
MIXED_MENTIONS_IN_THE_CORPUS = 13
PEOPLE_MENTIONED_IN_MIXED_MESSAGES = 11
THRESHOLD = 0.9


def _steady_probe() -> Probe:
    return Probe(clock=lambda: 0.0, memory=lambda: Memory(rss=0, peak=0))


def _three_people() -> list[Message]:
    """Authors p1 and p2; p3, p4 and p5 are mentioned twice each."""
    return [
        Message("p1", "NOME_A", "o [NOME_C|p3] falou com o [NOME_D|p4]"),
        Message("p2", "NOME_B", "o [NOME_C|p3] e o [NOME_D|p4] brigaram"),
        Message("p1", "NOME_A", "a [NOME_E|p5] viu tudo, a [NOME_E|p5] contou"),
    ]


def test_coreference_measurement_reports_precision_and_fusion_apart() -> None:
    messages = _three_people()
    conversation = join(messages)
    # prefix, NOME_C, NOME_D, prefix, NOME_C, NOME_D, prefix, NOME_E, NOME_E
    labels = [None, FIRST, FIRST, None, FIRST, SECOND, None, THIRD, THIRD]
    resolver = chains_by_label(conversation, labels)

    report = measure([messages], resolver, NO_SIZES, _steady_probe())

    # linked pairs: NOME_C-NOME_C and NOME_E-NOME_E right, both NOME_C with the first NOME_D wrong
    assert report.scores.precision == Ratio(2, 4)
    # people: NOME_C and NOME_D share a group, NOME_E does not
    assert report.scores.fusion == Ratio(2, 3)
    assert report.scores.recall == Ratio(2, 3)


def test_a_person_split_in_two_groups_lowers_recall_and_fuses_no_one() -> None:
    messages = _three_people()
    conversation = join(messages)
    labels = [None, FIRST, SECOND, None, THIRD, SECOND, None, FOURTH, FOURTH]
    resolver = chains_by_label(conversation, labels)

    report = measure([messages], resolver, NO_SIZES, _steady_probe())

    assert report.scores.precision == Ratio(2, 2)
    assert report.scores.fusion == Ratio(0, 3)
    assert report.scores.recall == Ratio(2, 3)


def test_measurement_covers_messages_mixing_languages() -> None:
    messages = [
        Message("p1", "NOME_A", "o [NOME_C|p3] chegou e o [NOME_D|p4] saiu"),
        Message("p2", "NOME_B", "ok, [NOME_C|p3] said he will call [NOME_D|p4]", mixed=True),
    ]
    conversation = join(messages)
    # prefix, NOME_C, NOME_D, prefix, NOME_C (mixed), NOME_D (mixed)
    labels = [None, FIRST, SECOND, None, SECOND, SECOND]
    resolver = chains_by_label(conversation, labels)

    report = measure([messages], resolver, NO_SIZES, _steady_probe())

    # five pairs touch the mixed message; linked: NOME_D with both mixed mentions (one right) and
    # the two mixed mentions with each other (wrong)
    assert report.scores.mixed_precision == Ratio(1, 3)
    assert report.scores.mixed_fusion == Ratio(2, 2)


def test_the_corpus_has_mentions_inside_messages_mixing_languages() -> None:
    conversations = [join(messages) for messages in load(CORPUS)]

    mixed = [
        (index, m)
        for index, c in enumerate(conversations)
        for m in c.mentions
        if m.mixed and not m.author_name
    ]

    assert len(mixed) == MIXED_MENTIONS_IN_THE_CORPUS
    assert len({(index, m.entity) for index, m in mixed}) == PEOPLE_MENTIONED_IN_MIXED_MESSAGES


def test_mixed_precision_below_the_threshold_fails_even_with_good_overall_precision() -> None:
    messages = [
        *[Message("p1", "NOME_A", "a [NOME_E|p5] e a [NOME_E|p5] e a [NOME_E|p5]")] * 3,
        Message("p2", "NOME_B", "[NOME_C|p3] met [NOME_D|p4]", mixed=True),
    ]
    conversation = join(messages)
    same_person = [None, FIRST, FIRST, FIRST]
    labels = [*same_person, *same_person, *same_person, None, SECOND, SECOND]
    report = measure([messages], chains_by_label(conversation, labels), NO_SIZES, _steady_probe())

    refusals = shortfalls(report.scores, threshold=THRESHOLD)

    assert report.scores.precision == Ratio(36, 37)
    assert refusals == ("precisão nas mensagens que misturam línguas: 0,000 abaixo de 0,900",)


def test_measurement_reports_the_link_to_authors_apart() -> None:
    messages = [
        Message("p1", "NOME_A", "[NOME_B|p2], o [NOME_C|p3] e o [NOME_C|p3] vêm"),
        Message("p2", "NOME_B", "a [NOME_B|p2] aqui sou eu, viu"),
    ]
    conversation = join(messages)
    # prefix NOME_A, NOME_B, NOME_C, NOME_C, prefix NOME_B, NOME_B
    labels = [FIRST, SECOND, THIRD, THIRD, SECOND, FIRST]
    resolver = chains_by_label(conversation, labels)

    report = measure([messages], resolver, NO_SIZES, _steady_probe())

    # the vocative goes to NOME_B's name; the second NOME_B goes to NOME_A's name
    assert report.scores.author_precision == Ratio(1, 2)
    assert report.scores.author_recall == Ratio(1, 2)
    assert report.scores.precision == Ratio(1, 1)


def test_a_mention_of_someone_else_linked_to_an_author_counts_against_author_precision() -> None:
    messages = [Message("p1", "NOME_A", "o [NOME_C|p3] chegou")]
    conversation = join(messages)
    resolver = chains_by_label(conversation, [FIRST, FIRST])

    report = measure([messages], resolver, NO_SIZES, _steady_probe())

    assert report.scores.author_precision == Ratio(0, 1)
    assert report.scores.author_recall == Ratio(0, 0)


def test_measurement_reports_time_and_memory_by_conversation_size() -> None:
    messages = [Message("p1", "NOME_A", "oi"), Message("p2", "NOME_B", "olá [NOME_A|p1]")]
    ticks = itertools.count(start=0.0, step=0.5)
    readings = itertools.count(start=1)
    probe = Probe(clock=lambda: next(ticks), memory=lambda: Memory(rss=next(readings), peak=100))
    resolver = chains_by_label(join(messages), [None, None, None])

    report = measure([messages], resolver, (3, 1), probe)

    assert report.loaded == Memory(rss=1, peak=100)
    assert report.sizes == (
        SizeRow(messages=1, chars=len("NOME_A: oi\n"), seconds=0.5, memory=Memory(2, 100)),
        SizeRow(
            messages=3,
            chars=len("NOME_A: oi\nNOME_B: olá NOME_A\nNOME_A: oi\n"),
            seconds=0.5,
            memory=Memory(3, 100),
        ),
    )


def _one_mixed_message(tmp_path: Path, text: str) -> Path:
    corpus = tmp_path / "corpus.json"
    message = f'{{"author": "p1", "name": "NOME_A", "text": "{text}", "mixed": true}}'
    corpus.write_text(f'{{"conversations": [{{"messages": [{message}]}}]}}', encoding="utf-8")
    return corpus


def _spans_of(text: str, word: str) -> tuple[Span, ...]:
    return tuple(Span(*found.span()) for found in re.finditer(word, text))


def _fuse_both(text: str) -> tuple[tuple[Span, ...], ...]:
    return ((*_spans_of(text, "NOME_C"), *_spans_of(text, "NOME_D")),)


def _link_each_name(text: str) -> tuple[tuple[Span, ...], ...]:
    return (_spans_of(text, "NOME_C"),)


def _fused_model(_model: str) -> Resolver:
    return _fuse_both


def _linking_model(_model: str) -> Resolver:
    return _link_each_name


def test_model_below_the_threshold_fails_the_measurement(
    monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str], tmp_path: Path
) -> None:
    corpus = _one_mixed_message(tmp_path, "o [NOME_C|p3] and o [NOME_D|p4]")
    monkeypatch.setattr(measure_coreference, "RESOLVERS", {"fused": _fused_model})
    monkeypatch.setattr(measure_coreference, "PROBE", _steady_probe())

    exit_code = measure_coreference.main(["fused", "m", "--corpus", str(corpus), "--sizes", "1"])

    assert exit_code == 1
    assert capsys.readouterr().err.splitlines()[-2:] == [
        "measure_coreference.py: reprovado: precisão: 0,000 abaixo de 0,950",
        "measure_coreference.py: reprovado: precisão nas mensagens que misturam línguas: "
        "0,000 abaixo de 0,950",
    ]


def test_model_that_links_nothing_fails_the_measurement() -> None:
    messages = [Message("p1", "NOME_A", "o [NOME_C|p3] e o [NOME_C|p3]", mixed=True)]
    resolver = chains_by_label(join(messages), [None, None, None])
    report = measure([messages], resolver, NO_SIZES, _steady_probe())

    refusals = shortfalls(report.scores, threshold=0.95)

    assert refusals == (
        "precisão: nenhum par ligado a medir",
        "precisão nas mensagens que misturam línguas: nenhum par ligado a medir",
    )


def test_model_at_the_threshold_passes_the_measurement(
    monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str], tmp_path: Path
) -> None:
    corpus = _one_mixed_message(tmp_path, "o [NOME_C|p3] and o [NOME_C|p3]")
    monkeypatch.setattr(measure_coreference, "RESOLVERS", {"linked": _linking_model})
    monkeypatch.setattr(measure_coreference, "PROBE", _steady_probe())

    exit_code = measure_coreference.main(["linked", "m", "--corpus", str(corpus), "--sizes", "1"])

    captured = capsys.readouterr()
    assert exit_code == 0
    assert json.loads(captured.out)["scores"]["precision"] == {"hits": 1, "total": 1}
    assert "reprovado" not in captured.err


def test_the_report_names_the_model_file_without_its_folder(
    monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str], tmp_path: Path
) -> None:
    corpus = _one_mixed_message(tmp_path, "o [NOME_C|p3] and o [NOME_C|p3]")
    monkeypatch.setattr(measure_coreference, "RESOLVERS", {"linked": _linking_model})
    monkeypatch.setattr(measure_coreference, "PROBE", _steady_probe())

    measure_coreference.main(["linked", "/home/PESSOA_1/modelos/coref.pt", "--corpus", str(corpus)])

    captured = capsys.readouterr()
    assert json.loads(captured.out)["model"] == "coref.pt"
    assert "PESSOA_1" not in captured.out + captured.err
