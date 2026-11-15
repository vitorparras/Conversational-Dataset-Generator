"""The .NET steps against a small project of its own: the analyzers fail the build, a badly
formatted file fails the format. They need the `dotnet` of the machine and skip without it."""

from __future__ import annotations

import shutil
import tempfile
import unittest
from pathlib import Path

import dotnet_steps
import scope
from steps.common import Context

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

    def test_a_clean_change_builds_and_is_formatted(self) -> None:
        ctx, reach = self.reached(CLEAN.replace("Green", "Blue"))

        self.assertTrue(dotnet_steps.build(ctx, reach).ok)
        self.assertTrue(dotnet_steps.format_check(ctx, reach).ok)

    def test_an_enum_switch_that_is_not_exhaustive_fails_the_build_naming_ide0072(self) -> None:
        ctx, reach = self.reached(ENUM_SWITCH)

        result = dotnet_steps.build(ctx, reach)

        self.assertFalse(result.ok)
        self.assertTrue(any("IDE0072" in line for line in result.details), result.details)

    def test_a_file_out_of_format_fails_the_format_step(self) -> None:
        ctx, reach = self.reached(BADLY_FORMATTED)
        restore = ctx.run(["dotnet", "restore", "core/src/Planted/Planted.csproj"], self.repo)
        self.assertTrue(restore.ok, restore.text)

        result = dotnet_steps.format_check(ctx, reach)

        self.assertFalse(result.ok)
