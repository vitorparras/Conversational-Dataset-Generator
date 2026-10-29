"""Tests for the workflow that publishes, per target, the executable and the service folder."""

from __future__ import annotations

import re
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


def uploaded_names(text: str) -> list[str]:
    """The artifact name of every upload step, in file order: `name:` under a step's `with:`."""
    return re.findall(r"^ {10}name: (.+)$", text, flags=re.MULTILINE)


def service_matrix(text: str) -> dict[str, str]:
    """The runner of each target the service job builds for: `- { target: T, os: R }` lines."""
    pairs = re.findall(r"- \{ target: (\S+), os: ([^ ]+) \}", text)
    return dict(pairs)


def service_job(text: str) -> str:
    """The text of the job that builds the service folder."""
    return text[text.index("  servico:") :]


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

    def test_uploads_the_core_of_each_target_and_fails_when_nothing_was_published(self) -> None:
        core = self.text[: self.text.index("  servico:")]

        self.assertEqual(uploaded_paths(core), ["publish/linux-x64", "publish/win-x64"])
        self.assertEqual(core.count("if-no-files-found: error"), len(package.TARGETS))
        self.assertEqual(core.count("retention-days: 30"), len(package.TARGETS))

    def test_the_service_folder_is_built_on_the_system_of_each_target(self) -> None:
        self.assertEqual(
            service_matrix(self.text), {"linux-x64": "ubuntu-latest", "win-x64": "windows-latest"}
        )
        self.assertEqual(tuple(service_matrix(self.text)), package.TARGETS)
        self.assertIn("runs-on: ${{ matrix.os }}", service_job(self.text))

    def test_the_service_folder_is_built_by_the_script_for_the_target_of_the_matrix(self) -> None:
        build = line_with(service_job(self.text), "service_folder.py")

        self.assertIn("--system ${{ matrix.target }}", build)
        self.assertIn("--model pt_core_news_md", build)
        self.assertIn("--output publish", build)

    def test_the_service_folder_is_uploaded_whole_and_the_job_fails_without_it(self) -> None:
        job = service_job(self.text)

        self.assertEqual(uploaded_paths(job), ["publish/language-service"])
        self.assertIn("if-no-files-found: error", job)
        self.assertIn("retention-days: 30", job)

    def test_publishes_four_artifacts_the_core_and_the_service_of_each_target(self) -> None:
        names = [
            name.replace("${{ matrix.target }}", target)
            for name in uploaded_names(self.text)
            for target in (package.TARGETS if "matrix" in name else ("",))
        ]

        self.assertEqual(
            names,
            ["nucleo-linux-x64", "nucleo-win-x64", "servico-linux-x64", "servico-win-x64"],
        )

    def test_only_reads_the_repository(self) -> None:
        self.assertIn("permissions:\n  contents: read\n", self.text)
        self.assertNotIn("write", self.text)


if __name__ == "__main__":
    unittest.main()
