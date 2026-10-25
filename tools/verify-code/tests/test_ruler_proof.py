"""Tests for the ruler proof: injected mechanisms and a synthetic copy, no dotnet, no uv."""

from __future__ import annotations

import subprocess
import tempfile
import unittest
from pathlib import Path

from steps import properties, ruler_proof, ruler_seed, test_service
from steps.common import CommandOutcome, Context, StepResult, failed, passed

from . import support

MARKER = ruler_seed.MARKER
AUTH = "def accepts(self):\n    return hmac.compare_digest(self.digest, other)\n"
PROPERTY_FAILURE = f"FAILED tests/properties/test_{MARKER}.py::test_{MARKER}"
SUITE_FAILURE = "1 teste(s) do serviço falharam"
NOTHING_RAN = "serviço: nenhuma propriedade executada; o passo não passa vazio"
PROPERTY_CASE = "propriedade falsa"
DEFECT_CASE = "defeito em módulo crítico"


def refused_property() -> StepResult:
    return failed("properties", "serviço: 1 propriedade(s) falharam", f"  | {PROPERTY_FAILURE}")


def refused_defect() -> StepResult:
    return failed("test_service", SUITE_FAILURE)


def accepted(name: str) -> StepResult:
    return passed(name, "tudo passou")


def synthetic_copy(_ctx: Context, parent: Path) -> StepResult | None:
    """A copy with the module the defect needs and the folder the property goes in."""
    copy = parent / ruler_proof.COPY
    support.write(copy, ruler_seed.DEFECT_FILE, AUTH)
    (copy / ruler_seed.PROPERTY_FILE).parent.mkdir(parents=True)
    return None


def mechanisms(
    properties_answers: list[StepResult], service_answers: list[StepResult]
) -> dict[str, ruler_proof.Mechanism]:
    """Mechanisms that answer by script, one answer per call, in order."""

    def scripted(answers: list[StepResult]) -> ruler_proof.Mechanism:
        return lambda _ctx: answers.pop(0)

    return {"properties": scripted(properties_answers), "test_service": scripted(service_answers)}


class RulerProofTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        repo = Path(self._tmp.name) / "repo"
        repo.mkdir()
        self.root = support.git_repository(repo)
        self.parents: list[Path] = []

    def make_parent(self) -> Path:
        parent = Path(tempfile.mkdtemp(prefix="ruler-proof-test-"))
        self.addCleanup(lambda: parent.exists() and ruler_proof.discard(parent))
        self.parents.append(parent)
        return parent

    def prove(
        self,
        scripted: dict[str, ruler_proof.Mechanism],
        populate: ruler_proof.Populate = synthetic_copy,
    ) -> StepResult:
        kit = ruler_proof.Kit(scripted, populate, self.make_parent)
        return ruler_proof.prove(support.context(self.root), kit)

    def status(self) -> str:
        done = subprocess.run(
            ["git", "status", "--porcelain"],
            cwd=self.root,
            capture_output=True,
            text=True,
            check=False,
        )
        return done.stdout

    def leftovers(self) -> list[Path]:
        return [parent for parent in self.parents if parent.exists()]

    def test_both_defects_refused_pass_and_say_what_was_planted_and_who_refused(self) -> None:
        result = self.prove(mechanisms([refused_property()], [refused_defect()]))

        self.assertTrue(result.ok, result.details)
        reported = [d.strip() for d in result.details if "semeado:" in d or "recusado por:" in d]
        self.assertEqual(
            reported,
            [
                f"semeado: {ruler_seed.SEEDS[0].what}, em {ruler_seed.PROPERTY_FILE}",
                f"recusado por: properties: {PROPERTY_FAILURE}",
                f"semeado: {ruler_seed.SEEDS[1].what}, em {ruler_seed.DEFECT_FILE}",
                f"recusado por: test_service: {SUITE_FAILURE}",
            ],
        )

    def test_a_false_property_the_ruler_accepts_fails_the_step(self) -> None:
        result = self.prove(mechanisms([accepted("properties")], [refused_defect()]))

        self.assertFalse(result.ok)
        self.assertIn(f"a régua aceitou {PROPERTY_CASE}", result.details)

    def test_a_critical_defect_no_mechanism_refuses_fails_the_step(self) -> None:
        both = mechanisms([refused_property(), accepted("properties")], [accepted("test_service")])

        result = self.prove(both)

        self.assertFalse(result.ok)
        self.assertIn(f"a régua aceitou {DEFECT_CASE}", result.details)

    def test_a_second_line_mechanism_that_refuses_the_defect_is_enough(self) -> None:
        second = failed("properties", "serviço: 2 propriedade(s) falharam")
        both = mechanisms([refused_property(), second], [accepted("test_service")])

        result = self.prove(both)

        self.assertTrue(result.ok, result.details)
        self.assertIn(
            "  recusado por: properties: serviço: 2 propriedade(s) falharam", result.details
        )

    def test_a_property_run_that_executed_nothing_is_not_a_refusal(self) -> None:
        nothing = failed("properties", NOTHING_RAN)

        result = self.prove(mechanisms([nothing], [refused_defect()]))

        self.assertFalse(result.ok)
        self.assertIn(f"a régua aceitou {PROPERTY_CASE}", result.details)

    def test_a_collection_error_that_names_the_property_is_not_a_refusal(self) -> None:
        error = failed("properties", "serviço: 1 propriedade(s) falharam", f"  | ERROR {MARKER}.py")

        result = self.prove(mechanisms([error], [refused_defect()]))

        self.assertFalse(result.ok)
        self.assertIn(f"a régua aceitou {PROPERTY_CASE}", result.details)

    def test_a_command_that_broke_is_not_a_refusal_of_the_defect(self) -> None:
        broke = failed("test_service", "`pytest` terminou com erro sem teste reprovado")
        both = mechanisms([refused_property(), failed("properties", NOTHING_RAN)], [broke])

        result = self.prove(both)

        self.assertFalse(result.ok)
        self.assertIn(f"a régua aceitou {DEFECT_CASE}", result.details)

    def test_mechanisms_that_accept_everything_fail_the_step_for_each_defect(self) -> None:
        silent = mechanisms(
            [accepted("properties"), accepted("properties")], [accepted("test_service")]
        )

        result = self.prove(silent)

        self.assertFalse(result.ok)
        self.assertEqual(sum(d.startswith("a régua aceitou") for d in result.details), 2)

    def test_the_second_defect_meets_a_copy_without_the_first(self) -> None:
        planted: list[bool] = []

        def spy(ctx: Context) -> StepResult:
            planted.append((ctx.root / ruler_seed.PROPERTY_FILE).exists())
            return refused_defect()

        scripted = mechanisms([refused_property()], [])
        scripted["test_service"] = spy

        self.prove(scripted)

        self.assertEqual(planted, [False])

    def test_the_defects_are_planted_only_in_the_copy(self) -> None:
        before = self.status()
        roots: list[Path] = []

        def spy(ctx: Context) -> StepResult:
            roots.append(ctx.root)
            return refused_defect()

        scripted = mechanisms([refused_property()], [])
        scripted["test_service"] = spy

        self.prove(scripted)

        self.assertEqual(self.status(), before)
        self.assertNotIn(self.root, roots)

    def test_the_copy_is_removed_after_success_and_after_failure(self) -> None:
        self.prove(mechanisms([refused_property()], [refused_defect()]))
        self.prove(mechanisms([accepted("properties")], [refused_defect()]))

        self.assertEqual(self.leftovers(), [])

    def test_the_copy_is_removed_even_when_a_mechanism_raises(self) -> None:
        def explode(_ctx: Context) -> StepResult:
            raise RuntimeError("boom")

        with self.assertRaises(RuntimeError):
            self.prove({"properties": explode, "test_service": explode})

        self.assertEqual(self.leftovers(), [])

    def test_the_marker_found_in_the_output_directory_fails_the_step(self) -> None:
        support.write(self.root, ".verify-code/package/leak.txt", f"x {MARKER} y")

        result = self.prove(mechanisms([refused_property()], [refused_defect()]))

        self.assertFalse(result.ok)
        self.assertIn(f"`{MARKER}` apareceu em .verify-code/", result.details)

    def test_a_copy_that_could_not_be_made_fails_the_step_and_removes_the_parent(self) -> None:
        result = self.prove(mechanisms([], []), lambda _ctx, _parent: failed("x", "clone falhou"))

        self.assertFalse(result.ok)
        self.assertIn("clone falhou", result.details)
        self.assertEqual(self.leftovers(), [])

    def test_a_seed_that_cannot_be_planted_fails_the_step(self) -> None:
        def empty_copy(_ctx: Context, parent: Path) -> StepResult | None:
            (parent / ruler_proof.COPY).mkdir()
            return None

        result = self.prove(mechanisms([refused_property()], [refused_defect()]), empty_copy)

        self.assertFalse(result.ok)
        self.assertTrue(any("não pôde ser plantada" in d for d in result.details))

    def test_the_failure_marks_are_the_ones_the_real_steps_print(self) -> None:
        suite = test_service.judge(test_service.SuiteRun(1, 1), CommandOutcome(False, ""))
        side = properties.problems(
            properties.SERVICE_SIDE, properties.PropertyEvidence(1, 1), CommandOutcome(False, "")
        )

        self.assertTrue(any(ruler_seed.reports_failures(d) for d in suite.details))
        self.assertTrue(any(ruler_seed.reports_failures(d) for d in side))
