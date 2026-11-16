"""The .NET steps against a small project of its own: the analyzers fail the build, a badly
formatted file fails the format. They need the `dotnet` of the machine and skip without it."""

from __future__ import annotations

import shutil
import tempfile
import unittest
from collections.abc import Sequence
from pathlib import Path

import dotnet_steps
import scope
from steps.common import CommandOutcome, Context

from . import support

PROPS = """<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);CS1591</NoWarn>
  </PropertyGroup>
</Project>
"""
EDITORCONFIG = (
    "[*.cs]\n"
    "dotnet_analyzer_diagnostic.category-Style.severity = error\n"
    "csharp_style_namespace_declarations = file_scoped:error\n"
)
PROJECT = '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup /></Project>\n'
CLEAN = "namespace Planted;\n\ninternal enum Color\n{\n    Red,\n    Green,\n}\n"
ENUM_SWITCH = (
    "namespace Planted;\n\n"
    "internal enum Color\n{\n    Red,\n    Green,\n}\n\n"
    "internal static class Painter\n{\n"
    "    internal static int Code(Color color) => color switch { Color.Red => 1, _ => 2 };\n}\n"
)
BADLY_FORMATTED = "namespace Planted;\n\ninternal enum Color{Red,Green}\n"
SOLUTION = '<Solution><Project Path="src/Planted/Planted.csproj" /></Solution>\n'
SOURCE = "core/src/Planted/Planted.cs"


@unittest.skipUnless(shutil.which("dotnet"), "sem o dotnet do SDK nesta máquina")
class DotnetStepsTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.repo = support.repository(Path(self._tmp.name))
        base = {
            "core/Directory.Build.props": PROPS,
            ".editorconfig": EDITORCONFIG,
            "core/Core.slnx": SOLUTION,
            "core/src/Planted/Planted.csproj": PROJECT,
            SOURCE: CLEAN,
        }
        support.commit(self.repo, base, "chore: projeto")
        support.git(self.repo, "branch", "-q", "base")
        support.git(self.repo, "checkout", "-q", "-b", "feature")

    def reached(self, source: str) -> tuple[Context, scope.Scope]:
        support.commit(self.repo, {SOURCE: source}, "fix: muda o fonte")
        ctx = support.context(self.repo, "base")
        return ctx, scope.compute(ctx)

    def test_a_clean_change_builds(self) -> None:
        ctx, reach = self.reached(CLEAN.replace("Green", "Blue"))

        self.assertTrue(dotnet_steps.build(ctx, reach).ok)

    def test_a_clean_change_is_formatted(self) -> None:
        ctx, reach = self.reached(CLEAN.replace("Green", "Blue"))

        self.assertTrue(dotnet_steps.format_check(ctx, reach).ok)

    def test_an_enum_switch_that_is_not_exhaustive_fails_the_build_naming_ide0072(self) -> None:
        ctx, reach = self.reached(ENUM_SWITCH)

        result = dotnet_steps.build(ctx, reach)

        self.assertFalse(result.ok)
        self.assertTrue(any("IDE0072" in line for line in result.details), result.details)

    def test_a_file_out_of_format_fails_the_format_step(self) -> None:
        ctx, reach = self.reached(BADLY_FORMATTED)
        restore = ctx.run(["dotnet", "restore", "core/Core.slnx"], self.repo)
        self.assertTrue(restore.ok, restore.text)

        result = dotnet_steps.format_check(ctx, reach)

        self.assertFalse(result.ok)
        self.assertTrue(any("Planted.cs" in line for line in result.details), result.details)


class OutputTests(unittest.TestCase):
    def test_colors_are_stripped_from_the_findings(self) -> None:
        text = "\x1b[31mA.cs(1,1): error IDE0072: falta caso\x1b[0m\nruído\n"

        self.assertEqual(
            ["A.cs(1,1): error IDE0072: falta caso"],
            dotnet_steps.findings(text, dotnet_steps.DIAGNOSTIC),
        )

    def test_without_a_match_the_first_lines_show_instead_of_silence(self) -> None:
        self.assertEqual(["boom"], dotnet_steps.findings("boom\n", dotnet_steps.DIAGNOSTIC))

    def test_the_project_file_is_the_one_found_in_the_tree(self) -> None:
        ctx = self.tree("A", "A.Tests")

        self.assertEqual("core/src/A/A.csproj", dotnet_steps.project_file(ctx, "A"))
        self.assertEqual(
            "core/tests/A.Tests/A.Tests.csproj", dotnet_steps.project_file(ctx, "A.Tests")
        )

    def test_the_failed_test_is_named_and_the_noise_around_it_is_not(self) -> None:
        reach = scope.Scope(False, ("A.Tests",), (), ("A.Tests",), (), ())
        noise = "Build succeeded.\n  passed Foo.Ok\n  failed Foo.Bar\n  skipped Foo.Later\n"

        result = dotnet_steps.tests(self.tree("A.Tests", output=noise, ok=False), reach)

        self.assertFalse(result.ok)
        self.assertEqual(
            ["A.Tests reprovou:", "failed Foo.Bar"], [line for line in result.details if line]
        )

    def test_projects_that_pass_are_counted_and_the_ones_left_out_are_said(self) -> None:
        reach = scope.Scope(False, ("A.Tests",), (), ("A.Tests",), ("B.Tests: banco",), ())

        result = dotnet_steps.tests(self.tree("A.Tests"), reach)

        self.assertTrue(result.ok)
        self.assertIn("fora: B.Tests: banco", result.details)

    def test_the_build_says_when_the_diff_reaches_the_whole_core(self) -> None:
        reach = scope.Scope(True, ("A", "B"), ("A",), (), (), ())

        result = dotnet_steps.build(self.tree("A"), reach)

        self.assertTrue(result.ok)
        self.assertIn("o core inteiro", " ".join(result.details))

    def test_the_build_of_a_narrow_diff_does_not_claim_the_whole_core(self) -> None:
        reach = scope.Scope(False, ("A",), ("A",), (), (), ())

        result = dotnet_steps.build(self.tree("A"), reach)

        self.assertNotIn("o core inteiro", " ".join(result.details))

    def test_the_format_says_when_it_ran_over_the_whole_solution(self) -> None:
        many = tuple(f"core/src/A/F{n}.cs" for n in range(dotnet_steps.MAX_INCLUDED + 1))
        seen: list[Sequence[str]] = []

        def run(command: Sequence[str], cwd: Path) -> CommandOutcome:
            seen.append(command)
            return CommandOutcome(True, "")

        ctx = Context(Path("."), Path("."), run)
        wide = dotnet_steps.format_check(ctx, scope.Scope(False, (), (), (), (), many))
        narrow = dotnet_steps.format_check(ctx, scope.Scope(False, (), (), (), (), many[:1]))

        self.assertIn("a solução inteira", " ".join(wide.details))
        self.assertNotIn("--include", seen[0])
        self.assertNotIn("a solução inteira", " ".join(narrow.details))
        self.assertIn("--include", seen[1])

    def tree(self, *names: str, output: str = "", ok: bool = True) -> Context:
        """A context over a temporary tree that holds the named projects, whose commands answer
        with ``output``."""
        folder = tempfile.TemporaryDirectory()
        self.addCleanup(folder.cleanup)
        root = Path(folder.name)
        for name in names:
            kind = "tests" if ".Tests" in name else "src"
            support.write(root, f"core/{kind}/{name}/{name}.csproj", PROJECT)

        def run(command: Sequence[str], cwd: Path) -> CommandOutcome:
            return CommandOutcome(ok, output)

        return Context(root, root, run)
