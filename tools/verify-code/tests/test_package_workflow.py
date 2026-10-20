"""Tests for the workflow that publishes the executable of each target as a workflow artifact."""

from __future__ import annotations

import unittest

from steps import package

from . import support

WORKFLOW = support.VERIFY_CODE.parents[1] / ".github" / "workflows" / "package.yml"


def triggers(text: str) -> list[str]:
    """The lines of the `on:` block, up to the first blank line."""
    lines = text.splitlines()
    start = lines.index("on:")
    return lines[start : lines.index("", start)]


def line_with(text: str, marker: str) -> str:
    """The first line of ``text`` that contains ``marker``."""
    return next(line for line in text.splitlines() if marker in line)


def loop_targets(text: str) -> tuple[str, ...]:
    """The targets the publishing loop goes through, in its order."""
    listed = line_with(text, "for target in").split(" in ", 1)[1]
    return tuple(listed.split(";", 1)[0].split())


def uploaded_paths(text: str) -> list[str]:
    """What every upload step sends, in file order."""
    lines = text.splitlines()
    return [line.split("path:", 1)[1].strip() for line in lines if line.strip().startswith("path:")]


class PackageWorkflowTests(unittest.TestCase):
    def setUp(self) -> None:
        self.text = WORKFLOW.read_text(encoding="utf-8")

    def test_runs_only_when_asked(self) -> None:
        self.assertEqual(triggers(self.text), ["on:", "  workflow_dispatch:"])

    def test_proves_the_package_step_before_publishing(self) -> None:
        proof = self.text.index("verify.py --step package")

        self.assertLess(proof, self.text.index("dotnet publish"))

    def test_publishes_the_targets_the_package_step_compares(self) -> None:
        self.assertEqual(loop_targets(self.text), package.TARGETS)

    def test_restores_locked_for_each_target(self) -> None:
        restore = line_with(self.text, "dotnet restore")

        self.assertIn(package.PROJECT, restore)
        self.assertIn("--locked-mode", restore)
        self.assertIn("-p:RuntimeIdentifier=$target", restore)

    def test_publishes_each_target_as_the_package_step_does(self) -> None:
        publish = line_with(self.text, "dotnet publish")

        self.assertIn("--no-restore --configuration Release", publish)
        self.assertIn("-p:ContinuousIntegrationBuild=true", publish)
        self.assertIn("--runtime $target --output publish/$target", publish)

    def test_uploads_each_target_and_fails_when_nothing_was_published(self) -> None:
        self.assertEqual(uploaded_paths(self.text), ["publish/linux-x64", "publish/win-x64"])
        self.assertEqual(self.text.count("if-no-files-found: error"), len(package.TARGETS))
        self.assertEqual(self.text.count("retention-days: 30"), len(package.TARGETS))

    def test_only_reads_the_repository(self) -> None:
        self.assertIn("permissions:\n  contents: read\n", self.text)
        self.assertNotIn("write", self.text)


if __name__ == "__main__":
    unittest.main()
