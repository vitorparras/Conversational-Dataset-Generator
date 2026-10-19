"""Judges `nosemgrep: <rule> <reason>` on a finding's line or above: it names the rule, says why in
three words or more, and never stands in core/src, where no analyzer is silenced."""

from __future__ import annotations

import re
from collections.abc import Sequence

from steps.semgrep_report import Finding

COMMENT = re.compile(
    r"(?:#|//)\s*nosemgrep(?::\s*(?P<rules>[\w.-]+(?:\s*,\s*[\w.-]+)*))?(?P<reason>.*)"
)
FORBIDDEN_FOLDER = "core/src/"
MIN_REASON_WORDS = 3


def comment_for(lines: Sequence[str], finding: Finding) -> re.Match[str] | None:
    """The dispensing comment at the end of the finding's line or alone on the line above."""
    padded = ["", *lines, ""]
    line, above = padded[finding.line], padded[finding.line - 1].lstrip()
    return COMMENT.search(line) or COMMENT.match(above)


def problem_of(comment: re.Match[str], finding: Finding) -> str:
    """Why the comment does not dispense the finding; empty when it does."""
    place = f"{finding.path}:{finding.line}"
    if finding.path.startswith(FORBIDDEN_FOLDER):
        return f"{place}: dispensa proibida em core/src; corrija o código ou a regra"
    named = {rule.strip().rsplit(".", 1)[-1] for rule in (comment["rules"] or "").split(",")}
    if finding.rule not in named:
        return f"{place}: a dispensa não nomeia a regra {finding.rule}"
    if len(comment["reason"].split()) < MIN_REASON_WORDS:
        return f"{place}: a dispensa de {finding.rule} não diz o motivo em pelo menos três palavras"
    return ""
