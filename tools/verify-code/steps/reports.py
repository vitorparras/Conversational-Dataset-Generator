"""Readers for what the test tools write: TRX (dotnet test) and JUnit XML (pytest)."""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

from steps.common import local_name, parse_xml

PASSED = "Passed"
FAILED = "Failed"
SKIPPED = "Skipped"


@dataclass(frozen=True)
class TestOutcome:
    """One executed (or skipped) test."""

    __test__ = False  # not a test class, whatever its name suggests

    name: str
    outcome: str
    #: The test assembly (TRX) or the dotted module (JUnit) that holds the test.
    source: str


def read_trx(path: Path) -> list[TestOutcome]:
    """Every test result of a TRX file. NotExecuted is how xUnit reports a skip."""
    root = parse_xml(path)
    sources: dict[str, str] = {}
    for element in root.iter():
        if local_name(element.tag) != "UnitTest":
            continue
        for child in element.iter():
            if local_name(child.tag) == "TestMethod":
                code_base = child.get("codeBase", "").replace("\\", "/")
                sources[element.get("id", "")] = Path(code_base).name
    outcomes: list[TestOutcome] = []
    for element in root.iter():
        if local_name(element.tag) != "UnitTestResult":
            continue
        raw = element.get("outcome", "")
        outcome = {"Passed": PASSED, "NotExecuted": SKIPPED}.get(raw, FAILED)
        outcomes.append(
            TestOutcome(element.get("testName", ""), outcome, sources.get(element.get("testId", ""), ""))
        )
    return outcomes


def read_junit(path: Path) -> list[TestOutcome]:
    """Every test case of a JUnit XML file."""
    outcomes: list[TestOutcome] = []
    for element in parse_xml(path).iter("testcase"):
        children = {child.tag for child in element}
        if "skipped" in children:
            outcome = SKIPPED
        elif children & {"failure", "error"}:
            outcome = FAILED
        else:
            outcome = PASSED
        outcomes.append(TestOutcome(element.get("name", ""), outcome, element.get("classname", "")))
    return outcomes
