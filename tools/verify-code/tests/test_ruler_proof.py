"""Tests for the ruler proof: injected mechanisms and a synthetic copy, no dotnet, no uv."""

from __future__ import annotations

import os
import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from steps import (
    floors,
    mutation_judge,
    mutation_service,
    properties,
    ruler_proof,
    ruler_seed,
    test_service,
)
from steps.areas_doc import Area
from steps.common import CommandOutcome, Context, StepResult, failed, passed
from steps.coverage_by_area import SUMMARY, Tally
from steps.mutant import Mutant, MutantStatus
from steps.mutation_policy import MutationPolicy

from . import floors_fixture, support

MARKER = ruler_seed.MARKER
AUTH = "def accepts(self):\n    return hmac.compare_digest(self.digest, other)\n"
WEAK_TARGET = "language-service/tests/unit/test_auth.py"
STRONG_TEST = "from language_service.auth import SecretVerifier\n\n\ndef test_it():\n    assert 1\n"
AREA = Area(ruler_seed.CRITICAL_AREA, ruler_seed.CRITICAL_MODULE, 90, True)
POLICY = MutationPolicy(20, 20, "ConfigureAwait")
PROPERTY_FAILURE = f"FAILED tests/properties/test_{MARKER}.py::test_{MARKER}"
SUITE_FAILURE = "1 teste(s) do serviço falharam"
NOTHING_RAN = "serviço: nenhuma propriedade executada; o passo não passa vazio"
PROPERTY_CASE = "propriedade falsa"
DEFECT_CASE = "defeito em módulo crítico"
FLOOR_CASE = "cobertura abaixo do piso"
MUTATION_CASE = "mutantes sobreviventes"
DOCUMENT = floors_fixture.DOCUMENT.replace("| Autenticação |", f"| {AREA.name} |")


def refused_property() -> StepResult:
    return failed("properties", "serviço: 1 propriedade(s) falharam", f"  | {PROPERTY_FAILURE}")


def refused_defect() -> StepResult:
    return failed("test_service", SUITE_FAILURE)


def refused_floor() -> StepResult:
    below = floors.judge(AREA, Tally(AREA.name, "90 %", 7, 13), None, False)
    return failed("floors", *below.failures)


def refused_mutation() -> StepResult:
    mutants = (Mutant("auth.py", 1, "x", MutantStatus.KILLED),) + (
        Mutant("auth.py", 2, "y", MutantStatus.SURVIVED),
    ) * 3
    judged = mutation_judge.judge_module(mutation_judge.ModuleResult(AREA, mutants), POLICY)
    return failed("mutation", *judged.failures, *judged.notes)


def accepted(name: str) -> StepResult:
    return passed(name, "tudo passou")


def synthetic_copy(_ctx: Context, parent: Path) -> StepResult | None:
    """A copy with the document, the module the defects need and the folders the seeds go in."""
    copy = parent / ruler_proof.COPY
    support.write_areas(copy, DOCUMENT)
    support.write(copy, ruler_seed.DEFECT_FILE, AUTH)
    support.write(copy, WEAK_TARGET, STRONG_TEST)
    support.write(copy, "language-service/tests/unit/test_other.py", "import os\n")
    (copy / ruler_seed.PROPERTY_FILE).parent.mkdir(parents=True)
    return None


def scripted(**answers: list[StepResult]) -> dict[str, ruler_proof.Mechanism]:
    """Mechanisms that answer by script, one answer per call, in order; the four by name."""
    every = {"properties": [], "test_service": [], "floors": [], "mutation": []} | answers
    return {name: (lambda _ctx, a=a: a.pop(0)) for name, a in every.items()}


def refusing(**changed: list[StepResult]) -> dict[str, ruler_proof.Mechanism]:
    """Mechanisms that refuse every seed the way the real steps would, but for ``changed``."""
    every = {
        "properties": [refused_property()],
        "test_service": [refused_defect(), accepted("test_service")],
        "floors": [refused_floor()],
        "mutation": [refused_mutation()],
    } | changed
    return scripted(**every)


class RulerProofTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        repo = Path(self._tmp.name) / "repo"
        repo.mkdir()
        self.root = support.git_repository(repo)
        self.parents: list[Path] = []
        self.environment = mock.patch.dict(os.environ, clear=False)
        self.environment.start()
        self.addCleanup(self.environment.stop)

    def make_parent(self) -> Path:
        parent = Path(tempfile.mkdtemp(prefix="ruler-proof-test-"))
        self.addCleanup(lambda: parent.exists() and ruler_proof.discard(parent))
        self.parents.append(parent)
        return parent

    def prove(
        self,
        mechanisms: dict[str, ruler_proof.Mechanism],
        populate: ruler_proof.Populate = synthetic_copy,
        platform: str = mutation_service.LINUX,
    ) -> StepResult:
        kit = ruler_proof.Kit(mechanisms, populate, self.make_parent, platform)
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

    def test_every_defect_refused_passes_and_says_what_was_planted_and_who_refused(self) -> None:
        result = self.prove(refusing())

        self.assertTrue(result.ok, result.details)
        reported = [d.strip() for d in result.details if "semeado:" in d or "recusado por:" in d]
        self.assertEqual(
            reported[0::2], [f"semeado: {s.what}, em {s.where}" for s in ruler_seed.SEEDS]
        )
        self.assertEqual(
            [line.split(": ", 2)[1] for line in reported[1::2]],
            ["properties", "test_service", "floors", "mutation"],
        )

    def test_the_proof_exercises_the_four_mechanisms_with_a_seed_each_must_refuse(self) -> None:
        wanted = {name for seed in ruler_seed.SEEDS for name in seed.mechanisms}

        self.assertEqual(wanted, {"properties", "test_service", "floors", "mutation"})
        self.assertEqual(set(ruler_proof.DEFAULT.mechanisms), wanted)

    def test_a_false_property_the_ruler_accepts_fails_the_step(self) -> None:
        result = self.prove(refusing(properties=[accepted("properties")]))

        self.assertFalse(result.ok)
        self.assertIn(f"a régua aceitou {PROPERTY_CASE}", result.details)

    def test_a_critical_defect_no_mechanism_refuses_fails_the_step(self) -> None:
        both = refusing(
            properties=[refused_property(), accepted("properties")],
            test_service=[accepted("test_service"), accepted("test_service")],
        )

        result = self.prove(both)

        self.assertFalse(result.ok)
        self.assertIn(f"a régua aceitou {DEFECT_CASE}", result.details)

    def test_a_second_line_mechanism_that_refuses_the_defect_is_enough(self) -> None:
        second = failed("properties", "serviço: 2 propriedade(s) falharam")
        both = refusing(
            properties=[refused_property(), second],
            test_service=[accepted("test_service"), accepted("test_service")],
        )

        result = self.prove(both)

        self.assertTrue(result.ok, result.details)
        self.assertIn(
            "  recusado por: properties: serviço: 2 propriedade(s) falharam", result.details
        )

    def test_a_property_run_that_executed_nothing_is_not_a_refusal(self) -> None:
        result = self.prove(refusing(properties=[failed("properties", NOTHING_RAN)]))

        self.assertFalse(result.ok)
        self.assertIn(f"a régua aceitou {PROPERTY_CASE}", result.details)

    def test_a_collection_error_that_names_the_property_is_not_a_refusal(self) -> None:
        error = failed("properties", "serviço: 1 propriedade(s) falharam", f"  | ERROR {MARKER}.py")

        result = self.prove(refusing(properties=[error]))

        self.assertFalse(result.ok)
        self.assertIn(f"a régua aceitou {PROPERTY_CASE}", result.details)

    def test_a_command_that_broke_is_not_a_refusal_of_the_defect(self) -> None:
        broke = failed("test_service", "`pytest` terminou com erro sem teste reprovado")
        both = refusing(
            properties=[refused_property(), failed("properties", NOTHING_RAN)],
            test_service=[broke, accepted("test_service")],
        )

        result = self.prove(both)

        self.assertFalse(result.ok)
        self.assertIn(f"a régua aceitou {DEFECT_CASE}", result.details)

    def test_a_floors_step_that_accepts_the_uncovered_code_fails_the_proof(self) -> None:
        result = self.prove(refusing(floors=[accepted("floors")]))

        self.assertFalse(result.ok)
        self.assertIn(f"a régua aceitou {FLOOR_CASE}", result.details)

    def test_a_floors_step_that_breaks_instead_of_judging_is_not_a_refusal(self) -> None:
        broke = failed("floors", "nenhum relatório de cobertura em .verify-code/: rode antes")

        result = self.prove(refusing(floors=[broke]))

        self.assertFalse(result.ok)
        self.assertIn(f"a régua aceitou {FLOOR_CASE}", result.details)

    def test_a_floors_refusal_of_another_area_is_not_the_one_the_defect_gives(self) -> None:
        other = failed("floors", "Serviço: medido 10,0 %, abaixo do piso de 80 % (1 de 10 linhas)")

        result = self.prove(refusing(floors=[other]))

        self.assertFalse(result.ok)
        self.assertIn(f"a régua aceitou {FLOOR_CASE}", result.details)

    def test_a_mutation_step_that_accepts_the_survivors_fails_the_proof(self) -> None:
        result = self.prove(refusing(mutation=[accepted("mutation")]))

        self.assertFalse(result.ok)
        self.assertIn(f"a régua aceitou {MUTATION_CASE}", result.details)

    def test_a_mutation_run_that_broke_is_not_a_refusal_by_the_ceiling(self) -> None:
        broke = failed(
            "mutation",
            f"{AREA.name} (`{AREA.prefix}`): nenhum teste de tests/ importa {AREA.prefix}",
            f"serviço (Python): o mutmut terminou com erro ({ruler_seed.CEILING_MARK})",
        )
        unchecked = failed("mutation", f"{AREA.name} (`{AREA.prefix}`): 5 mutantes não verificados")

        for step in (broke, unchecked):
            with self.subTest(step.details[0]):
                result = self.prove(refusing(mutation=[step]))

                self.assertFalse(result.ok)
                self.assertIn(f"a régua aceitou {MUTATION_CASE}", result.details)

    def test_the_mutation_seed_narrows_the_step_to_the_one_module(self) -> None:
        modules: list[str | None] = []

        def spy(ctx: Context) -> StepResult:
            modules.append(ctx.module)
            return refused_mutation()

        self.prove(refusing() | {"mutation": spy})

        self.assertEqual(modules, [ruler_seed.CRITICAL_MODULE])

    def test_the_mutation_seed_is_not_proved_where_mutmut_does_not_run(self) -> None:
        calls: list[str] = []

        def spy(_ctx: Context) -> StepResult:
            calls.append("mutation")
            return refused_mutation()

        result = self.prove(refusing() | {"mutation": spy}, platform="win32")

        self.assertTrue(result.ok, result.details)
        self.assertEqual(calls, [])
        self.assertTrue(any(d.startswith("AVISO — mutantes sobreviventes") for d in result.details))

    def test_a_seed_that_runs_everywhere_is_proved_on_every_system(self) -> None:
        result = self.prove(refusing(), platform="win32")

        self.assertIn(f"  recusado por: floors: {refused_floor().details[0]}", result.details)

    def test_mechanisms_that_accept_everything_fail_the_step_for_each_defect(self) -> None:
        silent = scripted(
            properties=[accepted("properties"), accepted("properties")],
            test_service=[accepted("test_service")] * 2,
            floors=[accepted("floors")],
            mutation=[accepted("mutation")],
        )

        result = self.prove(silent)

        self.assertFalse(result.ok)
        self.assertEqual(sum(d.startswith("a régua aceitou") for d in result.details), 4)

    def test_each_defect_meets_a_copy_without_the_ones_before_it(self) -> None:
        seen: dict[str, str] = {}

        def spy(name: str, answer: StepResult) -> ruler_proof.Mechanism:
            def look(ctx: Context) -> StepResult:
                auth = (ctx.root / ruler_seed.DEFECT_FILE).read_text(encoding="utf-8")
                test = (ctx.root / WEAK_TARGET).read_text(encoding="utf-8")
                seen[name] = f"{MARKER in auth}|{'not hmac' in auth}|{test == STRONG_TEST}"
                return answer

            return look

        self.prove(
            refusing()
            | {
                "floors": spy("floors", refused_floor()),
                "mutation": spy("mutation", refused_mutation()),
            }
        )

        self.assertEqual(seen, {"floors": "True|False|True", "mutation": "False|False|False"})

    def test_the_weak_tests_replace_only_the_tests_that_import_the_module(self) -> None:
        contents: list[str] = []

        def spy(ctx: Context) -> StepResult:
            contents.append((ctx.root / WEAK_TARGET).read_text(encoding="utf-8"))
            contents.append((ctx.root / "language-service/tests/unit/test_other.py").read_text())
            return refused_mutation()

        self.prove(refusing() | {"mutation": spy})

        self.assertEqual(contents, [ruler_seed.WEAK_TEST, "import os\n"])

    def test_the_defects_are_planted_only_in_the_copy(self) -> None:
        before = self.status()
        roots: list[Path] = []

        def recording(mechanism: ruler_proof.Mechanism) -> ruler_proof.Mechanism:
            def spy(ctx: Context) -> StepResult:
                roots.append(ctx.root)
                return mechanism(ctx)

            return spy

        self.prove({name: recording(m) for name, m in refusing().items()})

        self.assertEqual(self.status(), before)
        self.assertEqual(len(roots), 5)
        self.assertNotIn(self.root, roots)

    def test_the_copy_is_removed_after_success_and_after_failure(self) -> None:
        self.prove(refusing())
        self.prove(refusing(properties=[accepted("properties")]))

        self.assertEqual(self.leftovers(), [])

    def test_the_copy_is_removed_even_when_a_mechanism_raises(self) -> None:
        def explode(_ctx: Context) -> StepResult:
            raise RuntimeError("boom")

        with self.assertRaises(RuntimeError):
            self.prove({"properties": explode, "test_service": explode})

        self.assertEqual(self.leftovers(), [])

    def test_the_summary_of_the_job_is_not_written_while_the_defects_run_and_is_given_back(
        self,
    ) -> None:
        os.environ[SUMMARY] = "summary.md"
        inside: list[str | None] = []

        def spy(_ctx: Context) -> StepResult:
            inside.append(os.environ.get(SUMMARY))
            return refused_mutation()

        self.prove(refusing() | {"mutation": spy})

        self.assertEqual(inside, [None])
        self.assertEqual(os.environ[SUMMARY], "summary.md")

    def test_the_summary_is_given_back_when_a_mechanism_raises(self) -> None:
        os.environ[SUMMARY] = "summary.md"

        def explode(_ctx: Context) -> StepResult:
            raise RuntimeError("boom")

        with self.assertRaises(RuntimeError):
            self.prove({"properties": explode})

        self.assertEqual(os.environ[SUMMARY], "summary.md")

    def test_the_marker_found_in_the_output_directory_fails_the_step(self) -> None:
        support.write(self.root, ".verify-code/package/leak.txt", f"x {MARKER} y")

        result = self.prove(refusing())

        self.assertFalse(result.ok)
        self.assertIn(f"`{MARKER}` apareceu em .verify-code/", result.details)

    def test_a_copy_that_could_not_be_made_fails_the_step_and_removes_the_parent(self) -> None:
        result = self.prove(scripted(), lambda _ctx, _parent: failed("x", "clone falhou"))

        self.assertFalse(result.ok)
        self.assertIn("clone falhou", result.details)
        self.assertEqual(self.leftovers(), [])

    def test_a_seed_that_cannot_be_planted_fails_the_step(self) -> None:
        def empty_copy(_ctx: Context, parent: Path) -> StepResult | None:
            (parent / ruler_proof.COPY).mkdir()
            return None

        result = self.prove(refusing(), empty_copy)

        self.assertFalse(result.ok)
        self.assertEqual(sum("não pôde ser plantada" in d for d in result.details), 4)

    def test_the_failure_marks_are_the_ones_the_real_steps_print(self) -> None:
        suite = test_service.judge(test_service.SuiteRun(1, 1), CommandOutcome(False, ""))
        side = properties.problems(
            properties.SERVICE_SIDE, properties.PropertyEvidence(1, 1), CommandOutcome(False, "")
        )

        self.assertTrue(any(ruler_seed.reports_failures(d) for d in suite.details))
        self.assertTrue(any(ruler_seed.reports_failures(d) for d in side))

    def test_the_marks_of_floors_and_mutation_are_the_ones_the_real_steps_print(self) -> None:
        below = floors.judge(AREA, Tally(AREA.name, "90 %", 1, 2), None, False).failures
        mutants = (Mutant("auth.py", 2, "y", MutantStatus.SURVIVED),) * 3
        survived = mutation_judge.judge_module(mutation_judge.ModuleResult(AREA, mutants), POLICY)

        self.assertTrue(any(ruler_seed.names_the_area_below_floor(d) for d in below))
        self.assertTrue(
            any(ruler_seed.names_the_survivors_over_ceiling(d) for d in survived.failures)
        )
