"""Issue 33 CA-2 and CA-3: an exception is logged by reference and step; a success has fixed fields."""

import json
from collections.abc import Sequence

import pytest

from conftest import Harness
from language_service.contract import Occurrence
from support.sweep import SEEDED_TEXT, Destination, find_leaks

pytestmark = pytest.mark.privacy

BODY = {"text": SEEDED_TEXT, "language": "pt", "reference": "ref-42", "contract_version": "1"}


class Failing:
    """A recognizer whose exception carries the text, as a careless library message would."""

    model_version = "model-1"

    def recognize(self, text: str, language: str) -> Sequence[Occurrence]:
        raise RuntimeError(f"could not analyse {text!r}")


class OutsideTheText:
    """A recognizer that answers with offsets beyond the text."""

    model_version = "model-1"

    def recognize(self, text: str, language: str) -> Sequence[Occurrence]:
        return [
            Occurrence(
                start=0, end=len(text) + 1, kind="person", part_of_speech="PROPN", syntactic_role="nsubj"
            )
        ]


class Finding:
    """A recognizer that finds things, at offsets no text of the test is long enough to contain by chance."""

    model_version = "model-1"
    OFFSETS = ((48213, 48251), (48260, 48271))

    def recognize(self, text: str, language: str) -> Sequence[Occurrence]:
        kinds = ("person", "place")
        return [
            Occurrence(start=start, end=end, kind=kind, part_of_speech="PROPN", syntactic_role="nsubj")
            for (start, end), kind in zip(self.OFFSETS, kinds, strict=True)
        ]


def test_exception_is_logged_with_reference_and_step_and_without_the_text(
    make_harness: type[Harness], capfd: pytest.CaptureFixture[str]
) -> None:
    harness = make_harness(Failing())

    reply = harness.client.post("/analyze", json=BODY)

    assert reply.status_code == 500
    assert reply.json() == {"error": "internal_error", "reference": "ref-42"}
    (line,) = harness.log_lines
    assert json.loads(line) == {"reference": "ref-42", "step": "recognize", "outcome": "error:RuntimeError"}
    captured = capfd.readouterr()
    swept = [
        Destination("log", harness.sink.getvalue().encode()),
        Destination("response", reply.content),
        Destination("stdout", captured.out.encode()),
        Destination("stderr", captured.err.encode()),
    ]
    assert find_leaks(swept) == []


def test_offsets_outside_the_text_fail_at_the_check_step(make_harness: type[Harness]) -> None:
    harness = make_harness(OutsideTheText())

    reply = harness.client.post("/analyze", json=BODY)

    assert reply.status_code == 500
    (line,) = harness.log_lines
    assert json.loads(line) == {"reference": "ref-42", "step": "check", "outcome": "error:ValueError"}


def test_success_line_has_the_fixed_fields_and_nothing_else(make_harness: type[Harness]) -> None:
    harness = make_harness(Finding())
    text = SEEDED_TEXT + "x" * 50_000

    reply = harness.client.post("/analyze", json={**BODY, "text": text})

    assert reply.status_code == 200
    (line,) = harness.log_lines
    event = json.loads(line)
    assert set(event) == {
        "reference", "chars", "elapsed_ms", "model_version", "count_by_kind", "step", "outcome"
    }
    assert event["reference"] == "ref-42"
    assert event["chars"] == len(text)
    assert event["model_version"] == "model-1"
    assert event["count_by_kind"] == {"person": 1, "place": 1}
    assert (event["step"], event["outcome"]) == ("respond", "ok")


def test_the_offsets_do_not_reach_the_log(make_harness: type[Harness]) -> None:
    harness = make_harness(Finding())

    harness.client.post("/analyze", json={**BODY, "text": SEEDED_TEXT + "x" * 50_000})

    written = harness.sink.getvalue()
    for number in (str(value) for pair in Finding.OFFSETS for value in pair):
        assert number not in written
    for name in ("start", "end", "offsets", "occurrences"):
        assert name not in written

