"""Where one kind of finding lives inside a Trivy result."""

from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class Section:
    """A list inside a Trivy result, with the keys of its id and subject and what it is called."""

    key: str
    id_key: str
    subject_key: str
    kind: str
    # Only misconfiguration items carry pass/fail; elsewhere `Status` is the fix state.
    status_matters: bool = False
