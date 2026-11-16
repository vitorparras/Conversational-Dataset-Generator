"""Tests for the core built once: `build` packs it, `properties` and `browser` only run it."""

from __future__ import annotations

import unittest

from . import support

WORKFLOW = support.VERIFY_CODE.parents[1] / ".github" / "workflows" / "verify-code.yml"
CACHE = "cache-dependency-path: core/**/packages.lock.json"
DOTNET_JOBS = (
    "build",
    "dotnet",
    "properties",
    "browser",
    "package",
    "offline_start",
    "dependencies",
)
CONSUMERS = ("properties", "browser")
OTHER_WORKFLOWS = ("codeql.yml", "build-remoto.yml")


def job(name: str) -> str:
    return support.workflow_job(WORKFLOW.read_text(encoding="utf-8"), name)


class BuildArtifactWorkflowTests(unittest.TestCase):
    def test_every_job_with_the_dotnet_sdk_caches_the_packages_by_the_lock_files(self) -> None:
        for name in DOTNET_JOBS:
            with self.subTest(job=name):
                self.assertIn("cache: true", job(name))
                self.assertIn(CACHE, job(name))

    def test_the_other_workflows_that_restore_the_solution_cache_the_packages_too(self) -> None:
        for name in OTHER_WORKFLOWS:
            with self.subTest(workflow=name):
                text = (WORKFLOW.parent / name).read_text(encoding="utf-8")
                self.assertIn("cache: true", text)
                self.assertIn(CACHE, text)

    def test_the_ruler_proof_has_no_cache_because_it_restores_no_package(self) -> None:
        # setup-dotnet fails its post step when the cache folder was never created.
        self.assertNotIn("cache: true", job("ruler_proof"))

    def test_the_build_job_makes_the_build_with_the_step_of_the_ruler_and_packs_it(self) -> None:
        text = job("build")

        self.assertIn("verify.py --step build_core", text)
        self.assertIn("Core.Host.Tests.Browser.csproj -c Release --no-restore", text)
        self.assertIn("name: build-nucleo", text)
        self.assertIn("if-no-files-found: error", text)

    def test_the_consumers_wait_for_the_build_and_download_it(self) -> None:
        for name in CONSUMERS:
            with self.subTest(job=name):
                text = job(name)
                self.assertIn("needs: [changes, build]", text)
                self.assertIn("actions/download-artifact@", text)
                self.assertIn("name: build-nucleo", text)
                self.assertIn("tar -xf", text)

    def test_the_package_outlives_a_rerun_of_the_failed_jobs_a_few_days_later(self) -> None:
        self.assertIn("retention-days: 7", job("build"))

    def test_the_consumers_neither_restore_nor_build(self) -> None:
        for name in CONSUMERS:
            with self.subTest(job=name):
                text = job(name)
                self.assertNotIn("dotnet restore", text)
                self.assertNotIn("dotnet build", text)

    def test_the_browser_runs_the_release_build_without_building(self) -> None:
        text = job("browser")

        self.assertIn("-c Release --no-build", text)

    def test_the_mutation_matrix_is_left_as_it_was(self) -> None:
        self.assertNotIn("cache: true", job("mutation_module"))
