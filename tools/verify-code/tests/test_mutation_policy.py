"""Tests for the reader of the mutation rules of docs/operacao/014 and the critical list."""

from __future__ import annotations

import tempfile
import unittest
from dataclasses import replace
from pathlib import Path

from steps import mutation_policy
from steps.areas_doc import Area
from steps.mutation_policy import MutationPolicy

from . import support
from .mutation_samples import POLICY

REPOSITORY = support.VERIFY_CODE.parents[1]
LOGGING = Area("Registro", "Core.Platform.Logging", 90, critical=True)
PLATFORM = Area("Plataforma", "Core.Platform", 80)
CONTRACTS = Area("Contrato", "Core.Boundary.Contracts", None)


class MutationPolicyTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)

    def read(self, text: str) -> MutationPolicy | tuple[str, ...]:
        support.write(self.root, mutation_policy.DOCUMENT, text)
        return mutation_policy.read_policy(self.root)

    def test_the_section_gives_the_survivor_ceiling_and_the_minutes(self) -> None:
        policy = self.read(POLICY)

        self.assertEqual(policy, MutationPolicy(20, 20, "ConfigureAwait"))

    def test_a_changed_survivor_ceiling_in_the_document_is_read(self) -> None:
        policy = self.read(POLICY.replace("**20 % dos", "**15 % dos"))

        self.assertEqual(policy, MutationPolicy(15, 20, "ConfigureAwait"))

    def test_a_section_without_the_survivor_phrase_fails_naming_it(self) -> None:
        policy = self.read(POLICY.replace("**20 % dos mutantes gerados**", "poucos"))

        self.assertEqual(
            policy,
            (
                f"{mutation_policy.DOCUMENT}: a seção `## Regras` não diz "
                "`no máximo **N % dos mutantes gerados**`",
            ),
        )

    def test_a_changed_ignored_method_in_the_document_is_read(self) -> None:
        policy = self.read(POLICY.replace("`ConfigureAwait`", "`Outro`"))

        self.assertEqual(policy, MutationPolicy(20, 20, "Outro"))

    def test_a_section_without_the_ignored_method_fails_naming_it(self) -> None:
        policy = self.read(POLICY.replace("Stryker ignora as", "Stryker lê"))

        self.assertEqual(
            policy,
            (
                f"{mutation_policy.DOCUMENT}: a seção `## Regras` não diz "
                "`Stryker ignora as chamadas a `Método``",
            ),
        )

    def test_the_document_of_the_repository_ignores_configure_await(self) -> None:
        policy = mutation_policy.read_policy(REPOSITORY)

        assert isinstance(policy, MutationPolicy)
        self.assertEqual(policy.ignored_method, "ConfigureAwait")

    def test_a_document_without_the_section_fails(self) -> None:
        policy = self.read(POLICY.replace("## Regras", "## Outra"))

        self.assertEqual(
            policy,
            (
                f"{mutation_policy.DOCUMENT}: seção `## Regras` não encontrada; "
                "não há regra de mutação",
            ),
        )

    def test_a_phrase_after_the_next_heading_does_not_count(self) -> None:
        moved = POLICY.replace("## Regras\n\n", "## Regras\n\n## Outra\n\n")

        policy = self.read(moved)

        self.assertIsInstance(policy, tuple)

    def test_the_critical_list_holds_only_the_areas_marked_in_the_column(self) -> None:
        areas = (LOGGING, PLATFORM, CONTRACTS)

        found = mutation_policy.critical(areas)

        self.assertEqual(found, (LOGGING,))

    def test_a_critical_area_above_the_old_floor_is_still_critical(self) -> None:
        raised = replace(LOGGING, floor=95)

        found = mutation_policy.critical((raised, PLATFORM))

        self.assertEqual(found, (raised,))

    def test_an_area_with_the_old_critical_floor_but_not_marked_is_not_critical(self) -> None:
        unmarked = replace(LOGGING, critical=False)

        found = mutation_policy.critical((unmarked, PLATFORM))

        self.assertEqual(found, ())

    def test_the_document_of_the_repository_is_read(self) -> None:
        policy = mutation_policy.read_policy(REPOSITORY)

        self.assertIsInstance(policy, MutationPolicy)
