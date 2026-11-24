"""The coreference scores over many conversations, at the threshold edge and in the corner cases."""

from collections.abc import Callable

from coreference.corpus import Message, Span, join
from coreference.measurement import Probe, measure, shortfalls
from coreference.scoring import Ratio, Scores, groups
from coreference.sizing import Memory

from support.coreference_chains import FIRST, SECOND, THIRD, chains_by_label

NO_SIZES: tuple[int, ...] = ()
NOTHING = Ratio(0, 0)
THRESHOLD = 0.95
type Chains = tuple[tuple[Span, ...], ...]


def _steady_probe() -> Probe:
    return Probe(clock=lambda: 0.0, memory=lambda: Memory(rss=0, peak=0))


def _by_text(*answers: tuple[list[Message], list[str | None]]) -> Callable[[str], Chains]:
    """A resolver for many conversations: each joined text gets the chains of its own labels."""
    chains: dict[str, Chains] = {}
    for messages, labels in answers:
        conversation = join(messages)
        chains[conversation.text] = chains_by_label(conversation, labels)(conversation.text)
    return lambda text: chains[text]


def test_scores_of_many_conversations_add_hits_and_totals() -> None:
    three_people = [
        Message("p1", "NOME_A", "o [NOME_C|p3] falou com o [NOME_D|p4]"),
        Message("p2", "NOME_B", "o [NOME_C|p3] e o [NOME_D|p4] brigaram"),
        Message("p1", "NOME_A", "a [NOME_E|p5] viu tudo, a [NOME_E|p5] contou"),
    ]
    one_person = [Message("p1", "NOME_A", "o [NOME_F|p6] e o [NOME_F|p6]")]
    resolver = _by_text(
        (three_people, [None, FIRST, FIRST, None, FIRST, SECOND, None, THIRD, THIRD]),
        (one_person, [None, FIRST, FIRST]),
    )

    report = measure([three_people, one_person], resolver, NO_SIZES, _steady_probe())

    # 2/4, 2/3 and 2/3 in the first conversation; 1/1, 1/1 and 0/1 in the second
    assert report.scores.precision == Ratio(3, 5)
    assert report.scores.recall == Ratio(3, 4)
    assert report.scores.fusion == Ratio(2, 4)


def test_precision_exactly_at_the_threshold_passes() -> None:
    at_threshold = Ratio(19, 20)
    scores = Scores(at_threshold, NOTHING, NOTHING, at_threshold, NOTHING, NOTHING, NOTHING)

    assert shortfalls(scores, threshold=THRESHOLD) == ()


def test_fusion_counts_both_people_of_a_wrong_link_and_the_mixed_slice_on_its_own() -> None:
    messages = [
        Message("p1", "NOME_A", "[NOME_C|p3] e [NOME_D|p4]"),
        Message("p2", "NOME_B", "[NOME_E|p5] and [NOME_E|p5]", mixed=True),
    ]
    conversation = join(messages)
    labels = [None, FIRST, FIRST, None, SECOND, SECOND]

    report = measure([messages], chains_by_label(conversation, labels), NO_SIZES, _steady_probe())

    # the one wrong link joins NOME_C, the first of the pair, and NOME_D, the second
    assert report.scores.fusion == Ratio(2, 3)
    # the pairs touching the mixed message link only NOME_E with itself
    assert report.scores.mixed_fusion == Ratio(0, 3)


def test_the_time_of_a_size_is_the_time_of_analysing_its_text() -> None:
    messages = [Message("p1", "NOME_A", "oi"), Message("p2", "NOME_B", "olá [NOME_A|p1]")]
    now = [0.0]

    def analysis_taking_one_second_per_char(text: str) -> Chains:
        now[0] += len(text)
        return ()

    probe = Probe(clock=lambda: now[0], memory=lambda: Memory(rss=0, peak=0))

    report = measure([messages], analysis_taking_one_second_per_char, (1, 2), probe)

    assert [(row.chars, row.seconds) for row in report.sizes] == [(11, 11.0), (30, 30.0)]


def test_a_mention_takes_the_chain_of_the_shortest_predicted_span_containing_it() -> None:
    conversation = join([Message("p1", "NOME_A", "o [NOME_C|p3] e o [NOME_C|p3]")])
    _, first, second = conversation.mentions
    whole_text = (Span(0, len(conversation.text)),)
    each_mention = (first.span, second.span)

    assert groups(conversation, (whole_text, each_mention)) == (0, 1, 1)


def test_a_mention_in_a_group_joining_two_authors_is_not_linked_to_its_author() -> None:
    messages = [Message("p1", "NOME_A", "oi [NOME_B|p2]"), Message("p2", "NOME_B", "oi")]
    conversation = join(messages)

    report = measure(
        [messages], chains_by_label(conversation, [FIRST, FIRST, FIRST]), NO_SIZES, _steady_probe()
    )

    # the group holds both authors' names: the link to NOME_B is ambiguous, so it is not a hit
    assert report.scores.author_precision == Ratio(0, 1)
    assert report.scores.author_recall == Ratio(1, 1)
