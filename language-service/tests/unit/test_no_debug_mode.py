"""No debug or reload switch exists in the service's code: there is nothing to turn on."""

from pathlib import Path

from support.source_scan import forbidden_switches

TESTS = Path(__file__).resolve().parents[1]
SOURCE_TEXT = "\n".join(
    path.read_text(encoding="utf-8")
    for path in sorted((TESTS.parent / "src" / "language_service").rglob("*.py"))
)


def test_service_code_has_no_debug_or_reload_switch() -> None:
    assert forbidden_switches(SOURCE_TEXT) == ()


def test_scan_finds_a_debug_or_reload_switch() -> None:
    text = (TESTS / "fixtures" / "code_rules" / "debug_flag.py.txt").read_text(encoding="utf-8")

    found = forbidden_switches(text)

    assert found == ("reload", "debug")
