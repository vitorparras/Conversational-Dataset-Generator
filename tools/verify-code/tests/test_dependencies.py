"""Tests for the dependencies step as a whole: the register, the trees, the workflow job, paths."""

from __future__ import annotations

import json
import tempfile
import unittest
from datetime import date
from pathlib import Path

from steps import accepted_risks, dependencies, dependency_tree, service_audit
from steps.common import CommandOutcome, StepResult

from . import dependency_fixtures as fx
from . import support

TODAY = date(2026, 10, 16)
WORKFLOW = support.VERIFY_CODE.parents[1] / ".github" / "workflows" / "verify-code.yml"
DIAMOND = dependency_tree.Tree(
    ("app",), {"app": ("b", "a"), "a": ("c",), "b": ("leaf",), "c": ("leaf",)}
)


def job_of(text: str, name: str) -> str:
    """The text of one job of the workflow: from its key to the next blank line."""
    return text.split(f"\n  {name}:", 1)[1].split("\n\n", 1)[0]


class DependenciesStepTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = fx.locked_repository(Path(self._tmp.name))

    def audit(
        self, report: str, restore: CommandOutcome = support.SUCCESS
    ) -> tuple[StepResult, fx.ServiceRunner]:
        runner = fx.ServiceRunner(fx.report_folder(self.root), report, restore=restore)
        return dependencies.audit(support.context(self.root, runner), TODAY), runner

    def register(self, *risks: str) -> None:
        support.write(self.root, accepted_risks.FILE, "".join(risks))

    def test_an_expired_risk_fails_before_auditing_anything(self) -> None:
        self.register(fx.risk("CVE-0000-0002", "zqx-leaf", "2026-10-15"))

        result, runner = self.audit(fx.audit_report())

        self.assertFalse(result.ok)
        self.assertEqual(
            result.details, ("risco aceito venceu em 2026-10-15: CVE-0000-0002 em zqx-leaf",)
        )
        self.assertEqual(runner.commands, [])

    def test_a_risk_due_today_is_still_accepted_and_listed(self) -> None:
        self.register(
            fx.risk("PYSEC-0000-1", "zqx-leaf", "2026-10-16"),
            fx.risk("PYSEC-0000-3", "zqx-leaf", "2026-10-16"),
        )

        result, _ = self.audit(fx.audit_report(fx.PYTHON_VULNERABLE))

        self.assertTrue(result.ok, result.details)
        self.assertIn(
            "  - risco aceito até 2026-10-16: PYSEC-0000-1 em zqx-leaf", result.details[2]
        )

    def test_both_trees_are_audited_when_both_are_clean(self) -> None:
        result, _ = self.audit(fx.audit_report())

        self.assertTrue(result.ok, result.details)
        self.assertEqual(
            result.details,
            (
                "núcleo: árvore completa auditada, nada em aberto",
                "serviço: árvore completa auditada, nada em aberto",
            ),
        )

    def test_the_service_is_audited_even_when_the_core_fails(self) -> None:
        restore = CommandOutcome(False, fx.restore_error(self.root))

        result, _ = self.audit(fx.audit_report(fx.PYTHON_VULNERABLE), restore)

        self.assertFalse(result.ok)
        self.assertEqual(result.details[0], "núcleo: 1 vulnerabilidade(s) sem risco aceito")
        self.assertIn("serviço: 2 vulnerabilidade(s) sem risco aceito", result.details)

    def test_a_missing_service_tree_fails_even_when_the_core_is_clean(self) -> None:
        (self.root / service_audit.LOCK).unlink()

        result, _ = self.audit(fx.audit_report())

        self.assertFalse(result.ok)


class DependencyTreeTests(unittest.TestCase):
    def test_the_path_is_the_shortest_chain_from_a_root(self) -> None:
        self.assertEqual(dependency_tree.path_to(DIAMOND, "leaf"), ("app", "b", "leaf"))

    def test_names_are_compared_without_case(self) -> None:
        self.assertEqual(dependency_tree.path_to(DIAMOND, "LEAF"), ("app", "b", "leaf"))

    def test_a_package_outside_the_tree_has_no_path(self) -> None:
        self.assertEqual(dependency_tree.path_to(DIAMOND, "absent"), ())

    def test_a_cycle_does_not_loop(self) -> None:
        tree = dependency_tree.Tree(("a",), {"a": ("b",), "b": ("a",)})

        self.assertEqual(dependency_tree.path_to(tree, "z"), ())

    def test_a_nuget_lock_roots_at_direct_and_project_entries_of_every_framework(self) -> None:
        lock = {
            "dependencies": {
                "net8.0": {"Zqx.Direct": {"type": "Direct"}},
                "net10.0": {"zqx.lib": {"type": "Project", "dependencies": {"Zqx.Leaf": "1"}}},
            }
        }

        tree = dependency_tree.from_nuget_lock(json.dumps(lock))

        self.assertEqual(tree.roots, ("Zqx.Direct", "zqx.lib"))
        self.assertEqual(tree.edges["zqx.lib"], ("Zqx.Leaf",))

    def test_a_uv_lock_roots_at_the_project_and_follows_its_dev_group(self) -> None:
        tree = dependency_tree.from_uv_lock(fx.UV_LOCK)

        self.assertEqual(tree.roots, ("language-service",))
        self.assertEqual(tree.edges["language-service"], ("zqx-web", "zqx-test"))


class WorkflowTests(unittest.TestCase):
    def setUp(self) -> None:
        self.text = WORKFLOW.read_text(encoding="utf-8")
        self.job = job_of(self.text, "dependencies")

    def test_the_job_installs_the_pinned_audit_tools_and_runs_the_step(self) -> None:
        self.assertIn(f"-r {service_audit.REQUIREMENTS}", self.job)
        self.assertIn("uses: actions/setup-dotnet@", self.job)
        self.assertIn("verify.py --step dependencies", self.job)

    def test_the_job_runs_on_every_event_including_the_weekly_schedule(self) -> None:
        self.assertNotIn("if:", self.job)
        self.assertIn("schedule:\n    - cron:", self.text)
