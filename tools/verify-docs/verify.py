#!/usr/bin/env python3
"""Verifies the repository documentation. Runs in CI on every pull request.

Seven checks, in this order of severity (one module each, under checks/):

1. data        - personal data and secrets: document ids, contacts, keys, machine paths
2. references  - a path cited in the documentation that does not exist in the repository
3. links       - an internal link that does not resolve
4. authority   - every document says whether it binds or not
5. formatting  - simple consistency rules
6. size        - line ceiling per document
7. index       - every document appears in the index of its own folder

Usage:
    python tools/verify-docs/verify.py                    # everything
    python tools/verify-docs/verify.py --only data,links
    python tools/verify-docs/verify.py --list             # names of the checks
    python tools/verify-docs/verify.py --root some/dir    # verify another tree

No external dependency: standard library only.
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

from checks.indexes import check_indexes
from document import list_documents
from finding import Finding
from registry import INDEX, NAMES, PER_DOCUMENT

#: Default root: the repository that contains this script (tools/verify-docs/verify.py).
DEFAULT_ROOT = Path(__file__).resolve().parents[2]


def _document_findings(root: Path, path: Path, chosen: list[str]) -> list[Finding]:
    lines = path.read_text(encoding="utf-8").splitlines(keepends=True)
    return [
        found
        for name in chosen
        if name in PER_DOCUMENT
        for found in PER_DOCUMENT[name](root, path, lines)
    ]


def run(root: Path, chosen: list[str]) -> tuple[list[Path], list[Finding]]:
    """Runs the chosen checks over every document under the root."""
    documents = list_documents(root)
    findings: list[Finding] = []
    for path in documents:
        findings.extend(_document_findings(root, path, chosen))
    if INDEX in chosen:
        findings.extend(check_indexes(root))
    return documents, findings


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Verifica a documentação do repositório.")
    parser.add_argument("--only", help="verificações a rodar, separadas por vírgula")
    parser.add_argument("--list", action="store_true", help="lista as verificações")
    parser.add_argument(
        "--root",
        type=Path,
        default=DEFAULT_ROOT,
        help="raiz a verificar (padrão: a raiz deste repositório)",
    )
    return parser


def _requested(only: str) -> list[str]:
    return [name.strip() for name in only.split(",") if name.strip()]


def _report(documents: list[Path], findings: list[Finding], chosen: list[str]) -> int:
    if not findings:
        print(f"OK — {len(documents)} documentos, {len(chosen)} verificações, nenhum problema.")
        return 0
    per_check: dict[str, int] = {}
    for finding in findings:
        print(finding)
        per_check[finding.check] = per_check.get(finding.check, 0) + 1
    summary = " · ".join(f"{name}: {total}" for name, total in sorted(per_check.items()))
    print(f"\nFALHOU — {len(findings)} problemas em {len(documents)} documentos ({summary})")
    return 1


def main(argv: list[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    if args.list:
        print("\n".join(NAMES))
        return 0

    chosen = _requested(args.only) if args.only else list(NAMES)
    unknown = [name for name in chosen if name not in NAMES]
    if unknown:
        print(f"verificação desconhecida: {', '.join(unknown)}")
        return 2

    documents, findings = run(args.root.resolve(), chosen)
    return _report(documents, findings, chosen)


if __name__ == "__main__":
    sys.exit(main())
