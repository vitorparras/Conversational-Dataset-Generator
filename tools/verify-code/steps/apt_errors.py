"""The ``E:`` lines apt wrote when an image build failed, which docker's own message leaves out.

``docker build`` ends a failed ``RUN`` with ``failed to solve: process ... did not complete``, and
the reason, apt's ``E: Unable to locate package`` or ``E: Failed to fetch``, is a few lines above.
With ``--progress=plain`` each of those lines carries the step and the second, ``#7 5.12 E: ...``.
"""

from __future__ import annotations

import re

from steps.coverage_by_area import summarize

APT_ERROR = re.compile(r"^(?:#\d+\s+\d+(?:\.\d+)?\s+)?(E: .+)$")
NONE_FOUND = "o apt não escreveu nenhuma linha `E:`; a causa está no fim do log do build"


def apt_errors(text: str) -> tuple[str, ...]:
    """Each ``E:`` line of the build output, once, in the order apt wrote it."""
    matches = (APT_ERROR.match(line.strip()) for line in text.splitlines())
    return tuple(dict.fromkeys(match.group(1) for match in matches if match))


def summary(title: str, errors: tuple[str, ...]) -> str:
    """The Markdown for the job summary: the title and the lines apt gave, or that it gave none."""
    lines = errors or (NONE_FOUND,)
    return "\n".join((f"### {title}", "", "```", *lines, "```", ""))


def publish(title: str, text: str) -> tuple[str, ...]:
    """Writes the ``E:`` lines of ``text`` in the job summary and returns them for the report."""
    errors = apt_errors(text)
    summarize(summary(title, errors))
    return errors
