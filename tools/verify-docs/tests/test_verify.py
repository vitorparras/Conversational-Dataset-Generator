"""Tests for tools/verify-docs/verify.py. Standard library only.

Each test builds a throwaway repository in a temporary directory and points the
verifier at it with --root. All content is synthetic.
"""

import contextlib
import importlib.util
import io
import sys
import tempfile
import unittest
from pathlib import Path

_VERIFY_PATH = Path(__file__).resolve().parents[1] / "verify.py"
_spec = importlib.util.spec_from_file_location("verify", _VERIFY_PATH)
verify = importlib.util.module_from_spec(_spec)
sys.modules["verify"] = verify  # dataclasses resolves annotations through sys.modules
_spec.loader.exec_module(verify)

VALID_DOC = "# Documento sintético\n\n> Descrição. 2026-01-01.\n\nTexto sem problema.\n"


def run_verifier(root: Path, *extra: str) -> tuple[int, str]:
    """Runs the verifier against `root` and returns (exit code, printed output)."""
    buffer = io.StringIO()
    with contextlib.redirect_stdout(buffer):
        code = verify.main(["--root", str(root), *extra])
    return code, buffer.getvalue()


class VerifyDocsTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)
        (self.root / "docs").mkdir()

    def write(self, relative: str, content: str) -> None:
        target = self.root / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(content, encoding="utf-8", newline="\n")

    def test_clean_repository_passes(self) -> None:
        self.write("docs/001-valid.md", VALID_DOC)
        code, output = run_verifier(self.root)
        self.assertEqual(code, 0, output)
        self.assertIn("OK", output)

    def test_document_without_authority_line_fails(self) -> None:
        self.write("docs/001-no-authority.md", "# Sem autoridade\n\nTexto.\n")
        code, output = run_verifier(self.root)
        self.assertEqual(code, 1, output)
        self.assertIn("[authority]", output)
        self.assertIn("docs/001-no-authority.md", output)

    def test_secret_pattern_fails(self) -> None:
        token = "ghp_" + "a1B2c3D4e5F6g7H8i9J0"
        self.write("docs/001-secret.md", VALID_DOC + f"\nToken: {token}\n")
        code, output = run_verifier(self.root)
        self.assertEqual(code, 1, output)
        self.assertIn("[data]", output)

    def test_cited_path_that_does_not_exist_fails(self) -> None:
        self.write("docs/001-ghost.md", VALID_DOC + "\nVeja `docs/does-not-exist.md`.\n")
        code, output = run_verifier(self.root)
        self.assertEqual(code, 1, output)
        self.assertIn("[references]", output)
        self.assertIn("docs/does-not-exist.md", output)

    def test_cited_path_that_exists_passes(self) -> None:
        self.write("docs/001-valid.md", VALID_DOC)
        self.write("docs/002-cites.md", VALID_DOC + "\nVeja `docs/001-valid.md`.\n")
        code, output = run_verifier(self.root)
        self.assertEqual(code, 0, output)

    def test_broken_link_fails(self) -> None:
        self.write("docs/001-link.md", VALID_DOC + "\n[x](missing.md)\n")
        code, output = run_verifier(self.root)
        self.assertEqual(code, 1, output)
        self.assertIn("[links]", output)

    def test_document_over_line_limit_fails(self) -> None:
        body = "".join(f"Linha {n}.\n" for n in range(verify.LINE_LIMIT + 5))
        self.write("docs/001-long.md", VALID_DOC + "\n" + body)
        code, output = run_verifier(self.root)
        self.assertEqual(code, 1, output)
        self.assertIn("[size]", output)

    def test_only_restricts_the_checks(self) -> None:
        self.write("docs/001-no-authority.md", "# Sem autoridade\n\nTexto.\n")
        code, output = run_verifier(self.root, "--only", "links")
        self.assertEqual(code, 0, output)

    def test_unknown_check_name_is_rejected(self) -> None:
        code, output = run_verifier(self.root, "--only", "nonexistent")
        self.assertEqual(code, 2, output)

    def test_list_prints_the_english_check_names(self) -> None:
        code, output = run_verifier(self.root, "--list")
        self.assertEqual(code, 0)
        self.assertEqual(
            output.split(),
            ["data", "references", "links", "authority", "formatting", "size", "index"],
        )


if __name__ == "__main__":
    unittest.main()
