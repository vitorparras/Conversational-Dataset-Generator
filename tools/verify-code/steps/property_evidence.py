"""What a suite's own report says about its properties: executed, and how many of those failed."""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from xml.etree import ElementTree

TRX_EXECUTED = frozenset({"Passed", "Failed", "Error", "Timeout", "Aborted"})
TRX_BROKEN = frozenset({"Failed", "Error", "Timeout", "Aborted"})


@dataclass(frozen=True)
class PropertyEvidence:
    """Properties that ran according to the report, and the ones of those that broke."""

    executed: int
    failed: int


NONE = PropertyEvidence(0, 0)


def from_trx(paths: tuple[Path, ...]) -> PropertyEvidence:
    """The tests a TRX report says ran; the run was already filtered to the property trait."""
    outcomes = [
        result.get("outcome", "")
        for path in paths
        for result in ElementTree.parse(path).getroot().iter()
        if result.tag.endswith("}UnitTestResult")
    ]
    ran = [outcome for outcome in outcomes if outcome in TRX_EXECUTED]
    return PropertyEvidence(len(ran), sum(outcome in TRX_BROKEN for outcome in ran))


def from_junit(path: Path) -> PropertyEvidence:
    """The test cases a JUnit report says ran; the run was already filtered to the property mark."""
    cases = [
        {child.tag for child in case} for case in ElementTree.parse(path).getroot().iter("testcase")
    ]
    ran = [outcomes for outcomes in cases if "skipped" not in outcomes]
    return PropertyEvidence(len(ran), sum(bool(o & {"failure", "error"}) for o in ran))
