"""Tests for the judgement of one module's mutants against the survivor ceiling."""

from __future__ import annotations

import unittest
from unittest import mock

from steps import mutation_judge
from steps.areas_doc import Area
from steps.mutant import Mutant, MutantStatus
from steps.mutation_judge import LanguageRun, ModuleResult, judge_language, judge_module
from steps.mutation_policy import MutationPolicy

AREA = Area("Registro", "Core.Platform.Logging", 90)
POLICY = MutationPolicy(90, 20, 20)
KILLED = Mutant("a.cs", 1, "", MutantStatus.KILLED)
SURVIVED = Mutant("a.cs", 7, "Equality mutation: `a != b`", MutantStatus.SURVIVED)
BROKEN = Mutant("a.cs", 3, "", MutantStatus.NOT_COUNTED)
IGNORED = Mutant("a.cs", 5, "", MutantStatus.IGNORED)
UNPLACED = Mutant("a.py", 0, "`a` → `b`", MutantStatus.SURVIVED)
TEXT = Area("Texto", "Core.Boundary.Text", 90)
CEILING_SECONDS = 20 * 60
UNCHECKED = Mutant("a.cs", 4, "", MutantStatus.UNCHECKED)


def module(killed: int, survived: int, *others: Mutant) -> ModuleResult:
    return ModuleResult(AREA, (KILLED,) * killed + (SURVIVED,) * survived + others)


class MutationJudgeTests(unittest.TestCase):
    def test_nineteen_percent_of_survivors_passes(self) -> None:
        verdict = judge_module(module(81, 19), POLICY)

        self.assertEqual(verdict.failures, ())

    def test_twenty_percent_of_survivors_passes(self) -> None:
        verdict = judge_module(module(80, 20), POLICY)

        self.assertEqual(verdict.failures, ())

    def test_twenty_one_percent_of_survivors_fails(self) -> None:
        verdict = judge_module(module(79, 21), POLICY)

        expected = "21,0 % de sobreviventes passa do limite de 20 %"
        self.assertEqual(verdict.failures, (f"Registro (`Core.Platform.Logging`): {expected}",))

    def test_the_ceiling_comes_from_the_policy(self) -> None:
        verdict = judge_module(module(81, 19), MutationPolicy(90, 10, 20))

        self.assertEqual(len(verdict.failures), 1)

    def test_mutants_that_never_counted_stay_out_of_the_share(self) -> None:
        verdict = judge_module(module(4, 1, BROKEN, IGNORED), POLICY)

        expected = "Registro (`Core.Platform.Logging`): 5 gerados, 4 mortos, 1 sobreviventes"
        self.assertEqual(verdict.notes[0], f"{expected} (20,0 %)")

    def test_each_survivor_is_listed_by_file_line_and_change(self) -> None:
        verdict = judge_module(module(4, 1), POLICY)

        self.assertEqual(verdict.notes[1], "  sobrevivente: a.cs:7 — Equality mutation: `a != b`")

    def test_an_unchecked_mutant_fails(self) -> None:
        verdict = judge_module(module(5, 0, UNCHECKED), POLICY)

        expected = "Registro (`Core.Platform.Logging`): 1 mutantes não verificados"
        self.assertEqual(verdict.failures, (expected,))

    def test_a_module_with_no_mutant_generated_fails(self) -> None:
        verdict = judge_module(module(0, 0, BROKEN), POLICY)

        expected = "nenhum mutante gerado; o passo não passa vazio"
        self.assertEqual(verdict.failures, (f"Registro (`Core.Platform.Logging`): {expected}",))

    def test_mutants_out_of_the_share_are_counted_by_kind(self) -> None:
        verdict = judge_module(module(4, 0, BROKEN, IGNORED, IGNORED), POLICY)

        self.assertEqual(
            verdict.notes[1], "  fora da conta: 1 não compilam, 2 ignorados ou desligados"
        )

    def test_a_survivor_without_a_line_says_it_was_not_placed(self) -> None:
        verdict = judge_module(module(9, 0, UNPLACED), POLICY)

        self.assertEqual(
            verdict.notes[1], "  sobrevivente: a.py (linha não localizada) — `a` → `b`"
        )


class MutationCeilingRuleTests(unittest.TestCase):
    def test_time_exactly_at_the_ceiling_is_not_over_it(self) -> None:
        self.assertFalse(mutation_judge.over_ceiling(POLICY, CEILING_SECONDS))

    def test_one_second_past_the_ceiling_is_over_it(self) -> None:
        self.assertTrue(mutation_judge.over_ceiling(POLICY, CEILING_SECONDS + 1))

    def test_the_ceiling_minutes_come_from_the_policy(self) -> None:
        self.assertFalse(
            mutation_judge.over_ceiling(MutationPolicy(90, 20, 30), CEILING_SECONDS + 1)
        )

    def test_the_ceiling_finding_names_the_modules_and_the_time(self) -> None:
        found = mutation_judge.ceiling_problem(POLICY, ("A.B", "C"), 1290.0)

        self.assertEqual(found, "teto de 20 min excedido em `A.B`, `C`: 21,5 min gastos")

    def test_timed_returns_the_value_and_the_seconds_between_two_readings(self) -> None:
        with mock.patch("steps.mutation_judge.monotonic", side_effect=[10.0, 70.0]):
            measured = mutation_judge.timed(lambda: "ZQX")

        self.assertEqual((measured.value, measured.seconds), ("ZQX", 60.0))

    def test_a_language_problem_fails_named_by_its_side(self) -> None:
        run = LanguageRun("núcleo", problems=("ZQX quebrou",))

        verdict = judge_language(run, POLICY)

        self.assertEqual(verdict.failures, ("núcleo: ZQX quebrou",))

    def test_a_module_without_code_is_a_note_not_a_failure(self) -> None:
        run = LanguageRun("núcleo", absent=(TEXT,))

        verdict = judge_language(run, POLICY)

        expected = "Texto (`Core.Boundary.Text`): sem código; a mutação vale quando houver"
        self.assertEqual((verdict.failures, verdict.notes), ((), (expected,)))

    def test_the_time_spent_is_reported_against_the_ceiling(self) -> None:
        run = LanguageRun("núcleo", (module(1, 0),), seconds=90.0)

        verdict = judge_language(run, POLICY)

        self.assertEqual(verdict.notes[-1], "núcleo: 1,5 min de 20 min de teto")

    def test_module_failures_reach_the_language_verdict(self) -> None:
        run = LanguageRun("núcleo", (module(1, 1),))

        verdict = judge_language(run, POLICY)

        self.assertEqual(len(verdict.failures), 1)
