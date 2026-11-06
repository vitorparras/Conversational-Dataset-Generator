"""Tests for the critical modules a pull request reaches, on a synthetic repository.

Core: Alpha <- Beta <- Gamma (each references the one before), Delta alone; every test project
references its project and Core.Tests.Support. Service: auth imports logging_, proof stands alone.
"""

from __future__ import annotations

import contextlib
import io
import json
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from steps import mutation, reach

from . import support
from .test_changes import commit

verify = support.load_verify()
CSPROJ = "<Project><ItemGroup>{}</ItemGroup></Project>\n"
REFERENCE = '<ProjectReference Include="..{sep}{name}{sep}{name}.csproj" />'
SUPPORT = "Core.Tests.Support"
CORE = {
    "Core.Alpha": (),
    "Core.Beta": ("Core.Alpha",),
    "Core.Gamma": ("Core.Beta",),
    "Core.Delta": (),
}
AREAS = "\n".join(
    (
        support.TABLE_HEADER,
        *(f"| {n} | `{n}` | 90 % | sim | crítica |" for n in CORE),
        "| Auth | `language_service.auth` | 90 % | sim | segredo |",
        "| Log | `language_service.logging_` | 90 % | sim | registro |",
        "| Proof | `language_service.proof` | 90 % | sim | prova |",
        "| Serviço | `language_service` | 80 % | não | demais |",
        "",
    )
)
SERVICE = "language-service"
PACKAGE = f"{SERVICE}/src/language_service"
WHOLE_TRIGGERS = (
    "core/Directory.Build.props",
    "core/src/Core.Alpha/Directory.Build.props",
    "core/Directory.Packages.props",
    "global.json",
    "core/src/Core.Alpha/packages.lock.json",
    f"{SERVICE}/uv.lock",
    f"{SERVICE}/pyproject.toml",
    ".github/workflows/verify-code.yml",
    "tools/verify-code/steps/reach.py",
    "tools/verify-code/verify.py",
    "core/Core.slnx",
    "core/nuget.config",
    "scripts/unknown.sh",
    "core/tests/fixtures/data.json",
    f"{SERVICE}/tests/conftest.py",
    f"{SERVICE}/README.txt",
)


def project(references: tuple[str, ...]) -> str:
    return CSPROJ.format("".join(REFERENCE.format(name=r, sep="\\") for r in references))


class ReachTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.repo = support.git_repository(Path(self._tmp.name))
        support.write_areas(self.repo, AREAS)
        files = {SUPPORT_PROJECT: project(())}
        for name, references in CORE.items():
            files[f"core/src/{name}/{name}.csproj"] = project(references)
            files[f"core/src/{name}/Code.cs"] = "class C {}\n"
            tests = (name, SUPPORT)
            files[f"core/tests/{name}.Tests/{name}.Tests.csproj"] = project(tests)
        files.update(
            {
                f"{PACKAGE}/__init__.py": "",
                f"{PACKAGE}/auth/__init__.py": "from language_service import logging_\n",
                f"{PACKAGE}/auth/tokens.py": "import language_service.auth\n",
                f"{PACKAGE}/logging_/__init__.py": "",
                f"{PACKAGE}/proof/__init__.py": "",
                f"{PACKAGE}/proof/uses.py": "from .. import logging_\n",
                f"{SERVICE}/tests/test_auth.py": "from language_service.auth import tokens\n",
                f"{SERVICE}/tests/test_proof.py": "import language_service.proof\n",
                "docs/roadmap/001-x.md": "a plain document\n",
            }
        )
        commit(self.repo, files)
        support.git(self.repo, "branch", "-f", "main", "HEAD")
        support.git(self.repo, "checkout", "-q", "-b", "pull")

    def reached(self, files: dict[str, str]) -> tuple[set[str], list[str]]:
        commit(self.repo, files)
        names, lines = reach.select(support.context(self.repo, None, "main"))
        return set(names), lines

    def test_a_file_of_the_project_of_a_module_reaches_it_and_what_references_it(self) -> None:
        names, lines = self.reached({"core/src/Core.Beta/Code.cs": "class D {}\n"})

        self.assertEqual(names, {"Core.Beta", "Core.Gamma"})
        self.assertIn("- `Core.Beta`: arquivo de Core.Beta", lines)
        self.assertIn("- `Core.Gamma`: depende de Core.Beta, que mudou", lines)

    def test_a_file_of_the_bottom_of_the_graph_reaches_every_module_above_it(self) -> None:
        names, _ = self.reached({"core/src/Core.Alpha/Code.cs": "class D {}\n"})

        self.assertEqual(names, {"Core.Alpha", "Core.Beta", "Core.Gamma"})

    def test_a_module_that_does_not_reference_the_change_is_left_out(self) -> None:
        names, lines = self.reached({"core/src/Core.Delta/Code.cs": "class D {}\n"})

        self.assertEqual(names, {"Core.Delta"})
        self.assertEqual(len(lines), 1)

    def test_a_test_project_reaches_the_module_it_exercises_and_only_it(self) -> None:
        names, lines = self.reached({"core/tests/Core.Beta.Tests/Test.cs": "class T {}\n"})

        self.assertEqual(names, {"Core.Beta"})
        self.assertIn("- `Core.Beta`: o teste Core.Beta.Tests mudou", lines)

    def test_a_project_the_test_projects_share_reaches_every_module_they_exercise(self) -> None:
        names, _ = self.reached({f"core/tests/{SUPPORT}/Helper.cs": "class H {}\n"})

        self.assertEqual(names, set(CORE))

    def test_a_test_project_no_module_exercises_reaches_none_and_does_not_widen(self) -> None:
        text = project(("Core.Delta",))
        names, lines = self.reached(
            {"core/tests/Core.Tests.Architecture/Core.Tests.Architecture.csproj": text}
        )

        self.assertEqual((names, lines), (set(), ["nenhum módulo alcançado"]))

    def test_the_area_file_of_a_module_reaches_it_and_the_modules_that_hold_it(self) -> None:
        text = (self.repo / "docs/operacao/areas/language_service.auth.md").read_text("utf-8")
        names, lines = self.reached({"docs/operacao/areas/language_service.auth.md": text + "\n"})

        self.assertEqual(names, {"language_service.auth"})
        self.assertIn(
            "- `language_service.auth`: a área language_service.auth em docs/operacao/areas mudou",
            lines,
        )

    def test_a_package_reaches_its_module_and_the_modules_that_import_it(self) -> None:
        names, lines = self.reached({f"{PACKAGE}/logging_/__init__.py": "x = 1\n"})

        self.assertEqual(
            names, {"language_service.logging_", "language_service.auth", "language_service.proof"}
        )
        self.assertIn(
            "- `language_service.proof`: depende de language_service.logging_, que mudou", lines
        )
        self.assertIn(
            "- `language_service.auth`: depende de language_service.logging_, que mudou", lines
        )

    def test_a_test_that_imports_a_module_reaches_it(self) -> None:
        names, lines = self.reached(
            {f"{SERVICE}/tests/test_proof.py": "import language_service.proof  # x\n"}
        )

        self.assertEqual(names, {"language_service.proof"})
        self.assertIn("- `language_service.proof`: o teste tests/test_proof.py mudou", lines)

    def test_a_module_of_one_language_does_not_reach_the_other(self) -> None:
        names, _ = self.reached({f"{PACKAGE}/proof/__init__.py": "x = 1\n"})

        self.assertEqual(names, {"language_service.proof"})

    def test_a_plain_document_beside_code_does_not_widen_the_reach(self) -> None:
        files = {
            "docs/roadmap/001-x.md": "changed\n",
            "core/src/Core.Delta/Code.cs": "class D {}\n",
        }
        names, _ = self.reached(files)

        self.assertEqual(names, {"Core.Delta"})

    def test_a_document_the_code_reads_runs_the_whole_ruler(self) -> None:
        files = {
            "docs/roadmap/001-x.md": "changed\n",
            "core/src/Core.Delta/Read.cs": '"docs/roadmap/001-x.md"\n',
        }
        names, lines = self.reached(files)

        self.assertEqual(names, self.everything())
        self.assertTrue(lines[0].startswith("régua inteira:"))

    def everything(self) -> set[str]:
        names, _ = reach.select(support.context(self.repo, None, "pull"))
        return set(names)


class WholeRulerTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.repo = support.git_repository(Path(self._tmp.name))
        support.write_areas(self.repo, AREAS)
        commit(self.repo, {"core/src/Core.Alpha/Core.Alpha.csproj": project(()), "a.txt": "1\n"})
        support.git(self.repo, "branch", "-f", "main", "HEAD")
        support.git(self.repo, "checkout", "-q", "-b", "pull")

    def select(self, files: dict[str, str]) -> tuple[set[str], list[str]]:
        commit(self.repo, files)
        names, lines = reach.select(support.context(self.repo, None, "main"))
        return set(names), lines

    def test_each_trigger_of_the_whole_ruler_reaches_every_module(self) -> None:
        everything = {"Core.Alpha", "Core.Beta", "Core.Gamma", "Core.Delta"}
        for trigger in WHOLE_TRIGGERS:
            with self.subTest(trigger=trigger):
                support.git(self.repo, "reset", "-q", "--hard", "main")
                names, lines = self.select({trigger: f"{trigger}\n"})

                self.assertTrue(everything <= names, trigger)
                self.assertIn(trigger, lines[0])
                self.assertTrue(lines[0].startswith("régua inteira:"))

    def test_a_trigger_beside_a_file_of_one_module_still_runs_everything(self) -> None:
        names, _ = self.select(
            {"global.json": "{}\n", "core/src/Core.Alpha/Code.cs": "class C {}\n"}
        )

        self.assertTrue({"Core.Beta", "Core.Delta"} <= names)

    def test_an_empty_diff_runs_everything(self) -> None:
        names, lines = reach.select(support.context(self.repo, None, "pull"))

        self.assertTrue({"Core.Beta", "Core.Delta"} <= set(names))
        self.assertIn("vazio", lines[0])

    def test_a_base_that_does_not_exist_runs_everything(self) -> None:
        names, lines = reach.select(support.context(self.repo, None, "origin/nowhere"))

        self.assertTrue({"Core.Beta", "Core.Delta"} <= set(names))
        self.assertTrue(lines[0].startswith("régua inteira:"))


PARENT = ["language_service", "auth"]


class ImportTests(unittest.TestCase):
    def test_a_relative_import_is_resolved_from_the_package_of_the_module(self) -> None:
        source = (
            "from . import tokens\nfrom .tokens import x\n"
            "from ..logging_ import y\nfrom .. import proof\n"
        )

        found, opaque = reach.targets(PARENT, source)

        self.assertFalse(opaque)
        expected = {
            "language_service.auth",
            "language_service.auth.tokens",
            "language_service.logging_",
            "language_service.logging_.y",
            "language_service.proof",
        }
        self.assertTrue(expected <= found, found)

    def test_an_absolute_import_is_read_as_before(self) -> None:
        source = "import language_service.proof\nfrom os import path\n"

        found, opaque = reach.targets(PARENT, source)

        self.assertFalse(opaque)
        self.assertTrue({"language_service.proof", "os", "os.path"} <= found)

    def test_an_import_the_reader_cannot_follow_is_flagged(self) -> None:
        sources = (
            "import importlib\n",
            "import importlib.util\n",
            "from importlib import import_module\n",
            "x = __import__('language_service.auth')\n",
            "from ... import x\n",
            "def broken(:\n",
        )
        for source in sources:
            with self.subTest(source=source):
                self.assertTrue(reach.targets(PARENT, source)[1])

    def test_a_module_that_imports_through_importlib_runs_the_whole_ruler(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            repo = support.git_repository(Path(tmp))
            support.write_areas(repo, AREAS)
            commit(repo, {f"{PACKAGE}/__init__.py": "", f"{PACKAGE}/proof/__init__.py": ""})
            support.git(repo, "branch", "-f", "main", "HEAD")
            support.git(repo, "checkout", "-q", "-b", "pull")
            commit(repo, {f"{PACKAGE}/proof/__init__.py": "import importlib\n"})

            names, lines = reach.select(support.context(repo, None, "main"))

        self.assertIn("language_service.auth", names)
        self.assertIn("régua inteira", lines[0])


class CommandLineTests(unittest.TestCase):
    def main(self, *argv: str) -> tuple[int, str, str]:
        out, err = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            code = verify.main(["--mutation-modules", *argv])
        return code, out.getvalue(), err.getvalue()

    def test_without_a_base_every_module_runs_and_the_summary_says_why(self) -> None:
        code, out, err = self.main()

        self.assertEqual(code, 0)
        self.assertEqual(json.loads(out), list(mutation.names(support.VERIFY_CODE.parents[1])[0]))
        self.assertIn("todos os módulos", err)

    def test_with_a_base_only_the_reached_jobs_go_to_stdout_and_the_reasons_to_stderr(self) -> None:
        chosen = (("Core.Beta",), ["- `Core.Beta`: arquivo de Core.Beta"])
        with mock.patch.object(verify.reach, "select", return_value=chosen):
            code, out, err = self.main("--base", "origin/main")

        self.assertEqual((code, json.loads(out)), (0, ["Core.Beta"]))
        self.assertEqual(err.strip(), chosen[1][0])


SUPPORT_PROJECT = f"core/tests/{SUPPORT}/{SUPPORT}.csproj"


if __name__ == "__main__":
    unittest.main()
