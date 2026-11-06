"""Tests for the classification of a diff: which pull requests may skip the heavy jobs.

The cases build a synthetic git repository in a temporary directory; the last ones read the real
tree, to keep the documents the code reads from ever being taken for plain documentation.
"""

from __future__ import annotations

import contextlib
import io
import tempfile
import unittest
from collections.abc import Sequence
from pathlib import Path

from steps import changes
from steps.common import CommandOutcome, Context, RunCommand, run_process

from . import support

verify = support.load_verify()
REAL_ROOT = support.VERIFY_CODE.parents[1]
REAL_READERS = (
    "docs/operacao/areas/Core.Platform.Logging.md",
    "docs/operacao/017-teste-de-mutacao.md",
    "docs/privacidade/006-acesso.md",
)


def commit(repo: Path, files: dict[str, str]) -> None:
    for name, text in files.items():
        support.write(repo, name, text)
    support.git(repo, "add", ".")
    support.git(repo, "commit", "-q", "-m", "change")


class VerdictTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.repo = support.git_repository(Path(self._tmp.name))

    def verdict(self, run: RunCommand | None = None, base: str | None = "main") -> changes.Verdict:
        return changes.verdict(support.context(self.repo, run, base))

    def test_documents_nobody_reads_skip_the_heavy_jobs(self) -> None:
        commit(
            self.repo,
            {"docs/roadmap/001-x.md": "x\n", "docs/roadmap/README.md": "x\n", "README.md": "y\n"},
        )

        decision = self.verdict()

        self.assertFalse(decision.heavy, decision.reasons)
        self.assertEqual(decision.output, "heavy=false")

    def test_a_code_file_beside_documents_runs_everything(self) -> None:
        commit(self.repo, {"docs/roadmap/001-x.md": "x\n", "core/src/A.cs": "class A {}\n"})

        decision = self.verdict()

        self.assertTrue(decision.heavy)
        self.assertEqual(decision.output, "heavy=true")
        self.assertIn(
            "core/src/A.cs: não é documento Markdown de docs/ nem da raiz", decision.reasons
        )

    def test_a_file_that_is_not_markdown_runs_everything_even_under_docs(self) -> None:
        for name in ("docs/schema.json", "docs/figure.png", "docs/roadmap/page.html"):
            with self.subTest(name=name):
                commit(self.repo, {name: "x\n"})

                self.assertTrue(self.verdict().heavy)
                support.git(self.repo, "reset", "-q", "--hard", "main")

    def test_a_markdown_file_outside_docs_and_the_root_runs_everything(self) -> None:
        for name in ("core/NOTES.md", "tools/verify-code/README.md", ".github/PULL_REQUEST.md"):
            with self.subTest(name=name):
                commit(self.repo, {name: "x\n"})

                self.assertTrue(self.verdict().heavy)
                support.git(self.repo, "reset", "-q", "--hard", "main")

    def test_the_workflows_and_the_tools_run_everything(self) -> None:
        for name in (".github/workflows/verify-code.yml", "tools/verify-code/verify.py"):
            with self.subTest(name=name):
                commit(self.repo, {name: "x\n"})

                self.assertTrue(self.verdict().heavy)
                support.git(self.repo, "reset", "-q", "--hard", "main")

    def test_moving_a_code_file_into_docs_runs_everything(self) -> None:
        commit(self.repo, {"core/src/A.cs": "class A {}\n"})
        support.git(self.repo, "branch", "-f", "main", "HEAD")
        (self.repo / "docs" / "roadmap").mkdir(parents=True)
        support.git(self.repo, "mv", "core/src/A.cs", "docs/roadmap/A.md")
        support.git(self.repo, "commit", "-q", "-m", "move")

        decision = self.verdict()

        self.assertTrue(decision.heavy)
        self.assertIn(
            "core/src/A.cs: não é documento Markdown de docs/ nem da raiz", decision.reasons
        )

    def test_an_empty_diff_runs_everything(self) -> None:
        decision = self.verdict()

        self.assertTrue(decision.heavy)
        self.assertEqual(
            decision.reasons, ("o diff está vazio: nada prova que dispensar é seguro",)
        )

    def test_without_a_base_the_whole_ruler_runs(self) -> None:
        commit(self.repo, {"docs/roadmap/001-x.md": "x\n"})

        decision = self.verdict(base=None)

        self.assertTrue(decision.heavy)

    def test_a_base_that_does_not_exist_runs_everything(self) -> None:
        commit(self.repo, {"docs/roadmap/001-x.md": "x\n"})

        decision = self.verdict(base="origin/inexistente")

        self.assertTrue(decision.heavy)
        self.assertEqual(decision.reasons, ("`git diff origin/inexistente...HEAD` falhou",))

    def test_a_search_that_fails_for_any_reason_other_than_no_match_runs_everything(self) -> None:
        commit(self.repo, {"docs/roadmap/001-x.md": "x\n"})

        def search_breaks(command: Sequence[str], cwd: Path) -> CommandOutcome:
            if "grep" in command:
                return CommandOutcome(False, "fatal", 128)
            return run_process(command, cwd)

        decision = self.verdict(search_breaks)

        self.assertTrue(decision.heavy)
        self.assertIn(
            "docs/roadmap/001-x.md: código, teste ou ferramenta lê ou cita este documento",
            decision.reasons,
        )

    def test_a_document_the_tests_open_by_separate_segments_runs_everything(self) -> None:
        commit(
            self.repo,
            {
                "core/tests/PolicyTests.cs": 'Path.Combine(root, "docs", "zona", "006-regra.md")\n',
                "docs/zona/006-regra.md": "x\n",
            },
        )
        support.git(self.repo, "branch", "-f", "main", "HEAD")
        commit(self.repo, {"docs/zona/006-regra.md": "y\n"})

        decision = self.verdict()

        self.assertTrue(decision.heavy)

    def test_every_document_of_a_folder_the_tools_open_runs_everything(self) -> None:
        commit(
            self.repo,
            {
                "tools/verify-code/steps/areas.py": 'FOLDER = "docs/operacao/areas"\n',
                "docs/operacao/areas/Nova.md": "x\n",
            },
        )
        support.git(self.repo, "branch", "-f", "main", "HEAD")
        commit(self.repo, {"docs/operacao/areas/Nova.md": "y\n"})

        decision = self.verdict()

        self.assertTrue(decision.heavy)

    def test_a_citation_by_number_does_not_make_the_folder_code(self) -> None:
        commit(
            self.repo,
            {
                "tools/build/reproduce.py": '"""The reason is in docs/operacao/018; see it."""\n',
                "docs/operacao/018-razao.md": "x\n",
                "docs/operacao/019-outra.md": "x\n",
            },
        )
        support.git(self.repo, "branch", "-f", "main", "HEAD")
        commit(self.repo, {"docs/operacao/019-outra.md": "y\n"})

        decision = self.verdict()

        self.assertFalse(decision.heavy, decision.reasons)

    def test_a_root_document_that_a_tool_names_runs_everything(self) -> None:
        commit(
            self.repo, {"tools/x/read.py": 'OPEN = "CONTRIBUTING.md"\n', "CONTRIBUTING.md": "x\n"}
        )
        support.git(self.repo, "branch", "-f", "main", "HEAD")
        commit(self.repo, {"CONTRIBUTING.md": "y\n"})

        decision = self.verdict()

        self.assertTrue(decision.heavy)

    def test_the_readme_does_not_become_code_because_some_index_names_it(self) -> None:
        commit(
            self.repo,
            {
                "tools/x/read.py": 'INDEX = "docs/roadmap/README.md"\n',
                "tools/verify-code/tests/t.py": 'write(path, "README.md", "base")\n',
                "tools/verify-docs/tests/t.py": 'write("README.md")\n',
                "README.md": "x\n",
            },
        )
        support.git(self.repo, "branch", "-f", "main", "HEAD")
        commit(self.repo, {"README.md": "y\n"})

        decision = self.verdict()

        self.assertFalse(decision.heavy, decision.reasons)


class RealTreeTests(unittest.TestCase):
    """The documents that code reads today stay code for the classification."""

    def context(self) -> Context:
        return Context(REAL_ROOT, REAL_ROOT / ".verify-code", run_process, "main")

    def test_the_documents_the_ruler_reads_count_as_code(self) -> None:
        for path in REAL_READERS:
            with self.subTest(path=path):
                self.assertTrue(changes.read_by_code(self.context(), path))

    def test_the_patterns_of_a_root_document_ask_for_a_path_boundary(self) -> None:
        self.assertEqual(len(changes.reader_patterns("README.md")), 1)
        self.assertTrue(changes.reader_patterns("README.md")[0].startswith(changes.NOT_A_PATH))


class CommandLineTests(unittest.TestCase):
    def test_without_a_base_the_flag_prints_heavy_true_and_exits_zero(self) -> None:
        buffer = io.StringIO()

        with contextlib.redirect_stdout(buffer), contextlib.redirect_stderr(io.StringIO()):
            code = verify.main(["--changes"])

        self.assertEqual(code, 0)
        self.assertEqual(buffer.getvalue().strip(), "heavy=true")

    def test_the_flag_prints_only_the_output_line_on_stdout_and_the_reasons_on_stderr(self) -> None:
        out, err = io.StringIO(), io.StringIO()

        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            verify.main(["--changes", "--base", "origin/ref-que-nao-existe"])

        self.assertEqual(out.getvalue().strip(), "heavy=true")
        self.assertIn("falhou", err.getvalue())


class PathTests(unittest.TestCase):
    def test_documentation_is_markdown_under_docs_or_at_the_root(self) -> None:
        for path in ("docs/a.md", "docs/x/y/001-z.md", "README.md", "AGENTS.md"):
            with self.subTest(path=path):
                self.assertTrue(changes.is_documentation(path))

    def test_everything_else_is_not_documentation(self) -> None:
        paths = ("docs/a.txt", "docs", "docs.md/x", "core/a.md", ".github/a.md", "a/b.md", "x.py")
        for path in paths:
            with self.subTest(path=path):
                self.assertFalse(changes.is_documentation(path))

    def test_the_generic_name_is_not_searched_by_itself(self) -> None:
        patterns = changes.reader_patterns("docs/operacao/README.md")

        self.assertNotIn(r"README\.md", patterns)
        self.assertEqual(len(patterns), 2)

    def test_a_named_document_is_also_searched_by_its_file_name(self) -> None:
        patterns = changes.reader_patterns("docs/privacidade/006-acesso.md")

        self.assertIn(r"006-acesso\.md", patterns)


if __name__ == "__main__":
    unittest.main()
