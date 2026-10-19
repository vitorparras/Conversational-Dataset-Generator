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

_VERIFY_DIR = Path(__file__).resolve().parents[1]
_VERIFY_PATH = _VERIFY_DIR / "verify.py"
sys.path.insert(0, str(_VERIFY_DIR))  # the script imports its sibling modules by name
_spec = importlib.util.spec_from_file_location("verify", _VERIFY_PATH)
assert _spec is not None and _spec.loader is not None
verify = importlib.util.module_from_spec(_spec)
sys.modules["verify"] = verify  # dataclasses resolves annotations through sys.modules
_spec.loader.exec_module(verify)
size = importlib.import_module("checks.size")
formatting = importlib.import_module("checks.formatting")

BACKSLASH = chr(92)
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
        body = "".join(f"Linha {n}.\n" for n in range(size.LINE_LIMIT + 5))
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

    # --- personal data and machine paths -------------------------------------------

    def test_real_looking_email_fails(self) -> None:
        self.write("docs/001-email.md", VALID_DOC + "\nContato: pessoa@dominio-sintetico.invalid\n")
        code, output = run_verifier(self.root, "--only", "data")
        self.assertEqual(code, 1, output)
        self.assertIn("e-mail", output)

    def test_example_domain_email_passes(self) -> None:
        self.write("docs/001-email.md", VALID_DOC + "\nContato: pessoa@exemplo.invalido\n")
        code, output = run_verifier(self.root, "--only", "data")
        self.assertEqual(code, 0, output)

    def test_windows_machine_path_fails(self) -> None:
        path = "C:" + BACKSLASH + "Users" + BACKSLASH + "USUARIO_X"
        self.write("docs/001-win.md", VALID_DOC + f"\nArquivo em {path}\n")
        code, output = run_verifier(self.root, "--only", "data")
        self.assertEqual(code, 1, output)
        self.assertIn("caminho absoluto de máquina", output)

    def test_windows_machine_path_with_forward_slash_fails(self) -> None:
        self.write("docs/001-win.md", VALID_DOC + "\nArquivo em D:/Repos/USUARIO_X\n")
        code, output = run_verifier(self.root, "--only", "data")
        self.assertEqual(code, 1, output)

    def test_posix_home_path_fails(self) -> None:
        self.write("docs/001-posix.md", VALID_DOC + "\nArquivo em /home/usuario_x/notas\n")
        code, output = run_verifier(self.root, "--only", "data")
        self.assertEqual(code, 1, output)
        self.assertIn("caminho absoluto de máquina", output)

    def test_posix_users_path_fails(self) -> None:
        self.write("docs/001-posix.md", VALID_DOC + "\nArquivo em /Users/usuario_x/notas\n")
        code, output = run_verifier(self.root, "--only", "data")
        self.assertEqual(code, 1, output)

    def test_secret_inside_code_block_still_fails(self) -> None:
        token = "ghp_" + "a1B2c3D4e5F6g7H8i9J0"
        self.write("docs/001-fence.md", VALID_DOC + f"\n```\n{token}\n```\n")
        code, output = run_verifier(self.root, "--only", "data")
        self.assertEqual(code, 1, output)

    # --- formatting -----------------------------------------------------------------

    def test_line_over_column_limit_fails(self) -> None:
        long_line = "palavra " * 14 + "fim\n"
        self.write("docs/001-wide.md", VALID_DOC + "\n" + long_line)
        code, output = run_verifier(self.root, "--only", "formatting")
        self.assertEqual(code, 1, output)
        self.assertIn("colunas", output)

    def test_wide_table_row_and_link_line_are_exempt(self) -> None:
        wide = "x" * (formatting.COLUMN_LIMIT + 20)
        self.write("docs/001-table.md", VALID_DOC + f"\n| {wide} |\n[{wide}](001-table.md)\n")
        code, output = run_verifier(self.root, "--only", "formatting")
        self.assertEqual(code, 0, output)

    def test_trailing_space_fails(self) -> None:
        self.write("docs/001-space.md", VALID_DOC + "\nlinha com espaço \n")
        code, output = run_verifier(self.root, "--only", "formatting")
        self.assertEqual(code, 1, output)
        self.assertIn("espaço no fim da linha", output)

    def test_tab_fails(self) -> None:
        self.write("docs/001-tab.md", VALID_DOC + "\nlinha\tcom tab\n")
        code, output = run_verifier(self.root, "--only", "formatting")
        self.assertEqual(code, 1, output)
        self.assertIn("tab", output)

    def test_three_blank_lines_in_a_row_fail(self) -> None:
        self.write("docs/001-blank.md", VALID_DOC + "\n\n\nFim.\n")
        code, output = run_verifier(self.root, "--only", "formatting")
        self.assertEqual(code, 1, output)

    def test_missing_final_newline_fails(self) -> None:
        self.write("docs/001-eof.md", VALID_DOC.rstrip("\n"))
        code, output = run_verifier(self.root, "--only", "formatting")
        self.assertEqual(code, 1, output)
        self.assertIn("quebra de linha final", output)

    # --- folder index ---------------------------------------------------------------

    def test_numbered_document_missing_from_folder_index_fails(self) -> None:
        self.write("docs/sub/README.md", "# Índice\n\nSem itens.\n")
        self.write("docs/sub/001-orphan.md", VALID_DOC)
        code, output = run_verifier(self.root, "--only", "index")
        self.assertEqual(code, 1, output)
        self.assertIn("001-orphan.md existe e não está no índice", output)

    def test_index_citing_missing_document_fails(self) -> None:
        self.write("docs/sub/README.md", "# Índice\n\n- [x](001-gone.md)\n- [y](002-here.md)\n")
        self.write("docs/sub/002-here.md", VALID_DOC)
        code, output = run_verifier(self.root, "--only", "index")
        self.assertEqual(code, 1, output)
        self.assertIn("001-gone.md, que não existe", output)

    def test_listed_document_passes_the_index_check(self) -> None:
        self.write("docs/sub/README.md", "# Índice\n\n- [y](001-here.md)\n")
        self.write("docs/sub/001-here.md", VALID_DOC)
        code, output = run_verifier(self.root, "--only", "index")
        self.assertEqual(code, 0, output)

    # --- references: code blocks and non-paths --------------------------------------

    def test_path_inside_fenced_code_block_is_not_checked(self) -> None:
        self.write("docs/001-fence.md", VALID_DOC + "\n```\nVeja `docs/does-not-exist.md`\n```\n")
        code, output = run_verifier(self.root, "--only", "references")
        self.assertEqual(code, 0, output)

    def test_glob_template_and_placeholder_are_not_charged(self) -> None:
        text = "\n`docs/*.md` `docs/NNN-slug.md` `docs/<name>.md` `docs/{id}.md`\n"
        self.write("docs/001-patterns.md", VALID_DOC + text)
        code, output = run_verifier(self.root, "--only", "references")
        self.assertEqual(code, 0, output)

    # --- ignored directories --------------------------------------------------------

    def test_ignored_directories_are_not_verified(self) -> None:
        for ignored in (".specs", "tools", ".github"):
            self.write(f"{ignored}/note.md", "# Sem autoridade\n\nTexto.\n")
        self.write("docs/001-valid.md", VALID_DOC)
        code, output = run_verifier(self.root)
        self.assertEqual(code, 0, output)
        self.assertIn("1 documentos", output)

    def test_the_output_of_the_code_verifier_is_not_verified(self) -> None:
        self.write(".verify-code/coverage/by-area.md", "### Cobertura por área\n")
        self.write("docs/001-valid.md", VALID_DOC)
        code, output = run_verifier(self.root)
        self.assertEqual(code, 0, output)

    def test_non_ignored_directory_is_verified(self) -> None:
        self.write("other/note.md", "# Sem autoridade\n\nTexto.\n")
        code, output = run_verifier(self.root)
        self.assertEqual(code, 1, output)
        self.assertIn("other/note.md", output)


if __name__ == "__main__":
    unittest.main()
