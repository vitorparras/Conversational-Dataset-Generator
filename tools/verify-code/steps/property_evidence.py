"""What a suite's own report says about its properties: executed, and how many of those failed."""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from xml.etree import ElementTree

TRX_EXECUTED = frozenset({"Passed", "Failed", "Error", "Timeout", "Aborted"})
TRX_BROKEN = frozenset({"Failed", "Error", "Timeout", "Aborted"})
HARNESS_MARKER = ".Harness."
SERVICE_PROPERTIES_PREFIX = "tests.properties."


@dataclass(frozen=True)
class PropertyEvidence:
    """Properties that ran according to the report, and the ones of those that broke."""

    executed: int
    failed: int


NONE = PropertyEvidence(0, 0)


def trx_classes(root: ElementTree.Element) -> dict[str | None, str]:
    """The class name of every test definition of a TRX report, by test id."""
    methods = ((e, m) for e in root.iter() if e.tag.endswith("}UnitTest") for m in e)
    return {
        e.get("id"): m.get("className") or "" for e, m in methods if m.tag.endswith("}TestMethod")
    }


def from_trx(paths: tuple[Path, ...]) -> PropertyEvidence:
    """The tests of the .NET properties project, harness tests apart, from its TRX reports."""
    outcomes: list[str] = []
    for path in paths:
        root = ElementTree.parse(path).getroot()
        classes = trx_classes(root)
        results = (e for e in root.iter() if e.tag.endswith("}UnitTestResult"))
        outcomes += [
            r.get("outcome", "")
            for r in results
            if HARNESS_MARKER not in f"{classes.get(r.get('testId'), '')}."
        ]
    ran = [o for o in outcomes if o in TRX_EXECUTED]
    return PropertyEvidence(len(ran), sum(o in TRX_BROKEN for o in ran))


def from_junit(path: Path) -> PropertyEvidence:
    """The service tests under tests/properties from its JUnit report; skipped did not run."""
    executed = failed = 0
    for case in ElementTree.parse(path).getroot().iter("testcase"):
        if not case.get("classname", "").startswith(SERVICE_PROPERTIES_PREFIX):
            continue
        outcomes = {child.tag for child in case}
        if "skipped" in outcomes:
            continue
        executed += 1
        failed += bool(outcomes & {"failure", "error"})
    return PropertyEvidence(executed, failed)
