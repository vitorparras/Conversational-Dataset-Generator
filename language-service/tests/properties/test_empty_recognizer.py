"""Properties of the foundation phase, each citing the document it comes from."""

import json

from fastapi.testclient import TestClient
from hypothesis import given
from hypothesis import strategies as st

from conftest import SECRET, SECRET_HEADER, Harness
from language_service.logging_ import ALLOWED_FIELDS

HARNESS = Harness()
CLIENT = TestClient(HARNESS.app, headers={SECRET_HEADER: SECRET})

# Any text a conversation can hold: every Unicode scalar value, including emoji and combining marks.
TEXTS = st.text(alphabet=st.characters(codec="utf-8"), max_size=2000)
REFERENCES = st.from_regex(r"[A-Za-z0-9_-]{1,64}", fullmatch=True)


@given(text=TEXTS, reference=REFERENCES)
def test_empty_recognizer_finds_nothing_and_the_log_counts_code_points(text: str, reference: str) -> None:
    """For any text, the empty recognizer answers "analysed, nothing found" and the log says how long.

    Source: requirement 021 (what the log may hold) and decision 0018 (an empty list is the answer
    "analysed, nothing found"; offsets and sizes are in code points, which is what ``len`` counts).
    """
    HARNESS.sink.seek(0)
    HARNESS.sink.truncate()

    reply = CLIENT.post(
        "/analyze",
        json={"text": text, "language": "pt", "reference": reference, "contract_version": "1"},
    )

    assert reply.status_code == 200
    assert reply.json() == {"occurrences": [], "model_version": None}
    (line,) = HARNESS.log_lines
    event = json.loads(line)
    assert event["chars"] == len(text)
    assert event["reference"] == reference
    assert event["count_by_kind"] == {}
    assert set(event) <= ALLOWED_FIELDS
