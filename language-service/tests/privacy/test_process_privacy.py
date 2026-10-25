"""A real process that analysed a marked text leaves the marker nowhere it could be read again."""

import json
import subprocess
import sys
from collections.abc import Iterator
from http import HTTPStatus
from pathlib import Path

import pytest

from support import process
from support.observation import Evidence, leaks, snapshot
from support.process import Output, Place
from support.synthetic import MARKER

pytestmark = pytest.mark.integration

TEXT = f"before {MARKER} after"
BODY = {"text": TEXT, "language": "pt", "reference": "ref-1", "contract_version": "1"}
LOG_KEYS = {
    "level",
    "event",
    "reference",
    "chars",
    "elapsed_ms",
    "model_version",
    "count_by_kind",
    "outcome",
}


@pytest.fixture
def place(tmp_path: Path) -> Place:
    work, temp, home = tmp_path / "work", tmp_path / "temp", tmp_path / "home"
    for folder in (work, temp, home):
        folder.mkdir()
    return Place(work, temp, home)


@pytest.fixture
def analysed(place: Place) -> Iterator[tuple[int, Output]]:
    """The status of one marked ``/analyze`` and what the service wrote until it exited."""
    port = process.free_port()
    service = process.start(port, place)
    try:
        process.wait_for_health(port, service)
        status = process.post_analyze(port, BODY)
    finally:
        output = process.finish(service)
    yield status, output
    process.stop(service)


def evidence_of(output: Output, place: Place) -> Evidence:
    watched = {"work": place.work, "temp": place.temp, "home": place.home}
    return Evidence(output.stdout, output.stderr, snapshot(watched))


def test_marked_text_reaches_no_stream_and_no_file(
    analysed: tuple[int, Output], place: Place
) -> None:
    _, output = analysed

    assert leaks(evidence_of(output, place), MARKER.encode("utf-8")) == ()


def test_the_analysis_was_answered_before_the_scan(analysed: tuple[int, Output]) -> None:
    status, output = analysed

    assert (status, output.code) == (HTTPStatus.OK, 0)


def test_the_request_line_carries_only_the_fixed_fields(analysed: tuple[int, Output]) -> None:
    _, output = analysed

    lines = [json.loads(line) for line in output.stderr.decode("utf-8").splitlines()]
    request = next(line for line in lines if line["event"] == "analyzed")

    assert (set(request), request["reference"], request["chars"]) == (
        LOG_KEYS,
        "ref-1",
        len(TEXT),
    )


def test_the_service_creates_no_file_while_it_works(
    analysed: tuple[int, Output], place: Place
) -> None:
    evidence = evidence_of(analysed[1], place)

    assert tuple(evidence.files) == ()


@pytest.mark.parametrize(
    ("code", "destination"),
    [
        ("import sys; sys.stdout.write(sys.argv[1])", "stdout"),
        ("import sys; sys.stderr.write(sys.argv[1])", "stderr"),
        ("import sys, tempfile; open(tempfile.mkstemp()[1], 'w').write(sys.argv[1])", "temp/"),
        (
            "import sys, pathlib; (pathlib.Path.home() / 'note.txt').write_text(sys.argv[1])",
            "home/",
        ),
    ],
)
def test_scan_names_the_destination_a_leaky_program_wrote_to(
    place: Place, code: str, destination: str
) -> None:
    completed = subprocess.run(  # noqa: S603 the test starts its own interpreter with a fixed script
        [sys.executable, "-c", code, MARKER],
        check=False,
        capture_output=True,
        cwd=place.work,
        env=place.environment(),
    )
    output = Output(completed.returncode, completed.stdout, completed.stderr)

    found = leaks(evidence_of(output, place), MARKER.encode("utf-8"))

    assert [name[: len(destination)] for name in found] == [destination]
