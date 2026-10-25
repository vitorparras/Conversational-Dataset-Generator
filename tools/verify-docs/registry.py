"""The checks that run once per document, and the order they run in."""

from __future__ import annotations

from collections.abc import Callable
from pathlib import Path

from checks.authority import check_authority
from checks.data import check_data
from checks.formatting import check_formatting
from checks.identifiers import check_identifiers
from checks.links import check_links
from checks.references import check_references
from checks.size import check_size
from finding import Finding

CheckFunction = Callable[[Path, Path, list[str]], list[Finding]]

#: Run once per document, in this order.
PER_DOCUMENT: dict[str, CheckFunction] = {
    "data": check_data,
    "references": check_references,
    "identifiers": check_identifiers,
    "links": check_links,
    "authority": check_authority,
    "formatting": check_formatting,
    "size": check_size,
}

#: Per folder, not per document: runs after the documents loop (see checks.indexes).
INDEX = "index"

NAMES = (*PER_DOCUMENT, INDEX)
