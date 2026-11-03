"""What a suite's own report says about its properties: executed, failed, and which."""

from __future__ import annotations

import re
from dataclasses import dataclass
from pathlib import Path
from xml.etree import ElementTree
from xml.etree.ElementTree import Element

TRX_EXECUTED = frozenset({"Passed", "Failed", "Error", "Timeout", "Aborted"})
TRX_BROKEN = frozenset({"Failed", "Error", "Timeout", "Aborted"})
SEED = re.compile(r"CsCheck_Seed=(?P<seed>\S+)")
INNER = "----"  # xunit prefixes the exception an aggregate wraps with dashes, one level per depth
NO_MESSAGE = "o relatório não traz mensagem"


@dataclass(frozen=True)
class PropertyEvidence:
    """Properties that ran per the report, the ones of those that broke, and what each said."""

    executed: int
    failed: int
    failures: tuple[str, ...] = ()


NONE = PropertyEvidence(0, 0)


def describe(name: str, message: str) -> tuple[str, ...]:
    """The lines that name a broken property: name, first line of the message, cause and seed.

    The first line of a CsCheck failure says only that the property broke; the exception it wraps,
    on a line of its own, says why, and the seed alone does not replay a failure of time or load.
    """
    lines = [line.strip() for line in message.splitlines() if line.strip()]
    cause = next((line for line in lines[1:] if line.startswith(INNER)), None)
    seed = SEED.search(message)
    return (
        f"  propriedade reprovada: {name}",
        f"    mensagem: {lines[0] if lines else NO_MESSAGE}",
        *((f"    causa: {cause.lstrip('-').strip()}",) if cause else ()),
        *((f"    CsCheck_Seed={seed.group('seed')}",) if seed else ()),
    )


def _message_of(result: Element) -> str:
    texts = [node.text or "" for node in result.iter() if node.tag.endswith("}Message")]
    return texts[0] if texts else ""


def from_trx(paths: tuple[Path, ...]) -> PropertyEvidence:
    """The tests a TRX report says ran; the run was already filtered to the property trait."""
    results = [
        result
        for path in paths
        for result in ElementTree.parse(path).getroot().iter()
        if result.tag.endswith("}UnitTestResult")
    ]
    ran = [result for result in results if result.get("outcome", "") in TRX_EXECUTED]
    broken = [result for result in ran if result.get("outcome", "") in TRX_BROKEN]
    failures = tuple(
        line
        for result in broken
        for line in describe(result.get("testName", result.get("testId", "?")), _message_of(result))
    )
    return PropertyEvidence(len(ran), len(broken), failures)


def from_junit(path: Path) -> PropertyEvidence:
    """The test cases a JUnit report says ran; the run was already filtered to the property mark."""
    cases = [
        (case, {child.tag: child for child in case})
        for case in ElementTree.parse(path).getroot().iter("testcase")
    ]
    ran = [(case, outcomes) for case, outcomes in cases if "skipped" not in outcomes]
    broken = [(case, outcomes) for case, outcomes in ran if outcomes.keys() & {"failure", "error"}]
    failures = tuple(
        line
        for case, outcomes in broken
        for line in describe(
            f"{case.get('classname', '')}.{case.get('name', '?')}",
            (outcomes["failure"] if "failure" in outcomes else outcomes["error"]).get(
                "message", ""
            ),
        )
    )
    return PropertyEvidence(len(ran), len(broken), failures)
