"""Tests for the judgement of one module's mutants against the survivor ceiling."""

from __future__ import annotations

import unittest

from steps.areas_doc import Area
from steps.mutant import Mutant, MutantStatus
from steps.mutation_judge import ModuleResult, judge_module
from steps.mutation_policy import MutationPolicy

AREA = Area("Registro", "Core.Platform.Logging", 90)
POLICY = MutationPolicy(90, 20, 20)
KILLED = Mutant("a.cs", 1, "", MutantStatus.KILLED)
SURVIVED = Mutant("a.cs", 7, "Equality mutation: `a != b`", MutantStatus.SURVIVED)
IGNORED = Mutant("a.cs", 3, "", MutantStatus.NOT_COUNTED)
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
        verdict = judge_module(module(4, 1, IGNORED, IGNORED), POLICY)

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
        verdict = judge_module(module(0, 0, IGNORED), POLICY)

        expected = "nenhum mutante gerado; o passo não passa vazio"
        self.assertEqual(verdict.failures, (f"Registro (`Core.Platform.Logging`): {expected}",))
