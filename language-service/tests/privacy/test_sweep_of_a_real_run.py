"""Issue 33 CA-1 and CA-4, requirement 021: seed material, cross the real server, sweep everything.

The server runs as a child process with its temporary and working directories pointed at empty
folders, so the sweep sees every file it could have left. The negative proof runs the same
procedure on a service that does leak, and the sweep has to fail naming the destination.
"""

import json
from pathlib import Path

import pytest

from language_service.logging_ import ALLOWED_FIELDS
from support.server import RunningService
from support.sweep import SEEDED_TEXT, collect, files_left_behind, find_leaks

pytestmark = [pytest.mark.privacy, pytest.mark.integration]

SUPPORT = str(Path(__file__).resolve().parent.parent / "support")


def _body(text: str = SEEDED_TEXT, **overrides: object) -> dict[str, object]:
    return {"text": text, "language": "pt", "reference": "ref-1", "contract_version": "1", **overrides}


def drive(service: RunningService) -> dict[str, int]:
    """Send the seeded text down the success path and every error path a caller can provoke."""
    statuses: dict[str, int] = {}
    with service.client() as client:
        statuses["success"] = client.post("/analyze", json=_body()).status_code
        statuses["extra-field"] = client.post("/analyze", json=_body(confidence=0.9)).status_code
        statuses["wrong-type"] = client.post("/analyze", json=_body(language=SEEDED_TEXT)).status_code
        statuses["old-contract"] = client.post("/analyze", json=_body(contract_version="0")).status_code
        statuses["malformed-json"] = client.post(
            "/analyze",
            content=('{"text": "' + SEEDED_TEXT + '", oops').encode("utf-8"),
            headers={"content-type": "application/json"},
        ).status_code
        statuses["too-long"] = client.post("/analyze", json=_body(SEEDED_TEXT * 100_000)).status_code
        statuses["bad-reference"] = client.post("/analyze", json=_body(reference=SEEDED_TEXT)).status_code
        statuses["unknown-route"] = client.post("/" + "x" * 10, json=_body()).status_code
    with service.client(secret="not-the-secret-" + "0" * 40) as intruder:
        statuses["no-secret"] = intruder.post("/analyze", json=_body()).status_code
    return statuses


def test_nothing_of_the_material_survives_the_request() -> None:
    service = RunningService()
    try:
        with service:
            statuses = drive(service)
        leaks = find_leaks(collect(service))
        left = files_left_behind(service)
    finally:
        service.dispose()

    assert statuses == {
        "success": 200,
        "extra-field": 400,
        "wrong-type": 400,
        "old-contract": 400,
        "malformed-json": 400,
        "too-long": 400,
        "bad-reference": 400,
        "unknown-route": 404,
        "no-secret": 401,
    }
    assert leaks == []
    assert left == [], "the service left files behind"


def test_every_line_the_service_wrote_has_only_fixed_fields() -> None:
    service = RunningService()
    try:
        with service:
            drive(service)
        lines = [line for line in service.log_file.read_text(encoding="utf-8").splitlines() if line]
        stdout_lines = [line for line in service.stdout_text.splitlines() if line]
    finally:
        service.dispose()

    assert lines, "the log file is empty"
    assert lines == stdout_lines, "the file and the standard output carry the same events"
    for line in lines:
        assert set(json.loads(line)) <= ALLOWED_FIELDS
    assert service.stderr_text == ""


@pytest.mark.parametrize(
    ("target", "expected"),
    [
        ("stdout", "stdout"),
        ("stderr", "stderr"),
        ("log-file", "log-file"),
        ("temp-dir", "temp-dir:scratch.txt"),
        ("work-dir", "work-dir:scratch.txt"),
        ("escaped-fragment", "stdout"),
    ],
)
def test_a_service_that_leaks_fails_the_sweep_and_the_destination_is_named(target: str, expected: str) -> None:
    service = RunningService(
        module="leaking_service",
        environment={"PYTHONPATH": SUPPORT, "LEAK_TO": target},
    )
    try:
        with service:
            drive(service)
        leaks = find_leaks(collect(service))
    finally:
        service.dispose()

    assert leaks, f"the sweep did not notice the text written to {target}"
    assert any(leak.startswith(expected + " ") for leak in leaks), leaks
