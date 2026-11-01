"""Tests for the mutation step run for one module, as each job of the CI matrix runs it."""

from __future__ import annotations

import contextlib
import io
import json
import unittest
from unittest import mock

from steps import areas_doc, mutation

from . import mutation_fixture as fixture
from . import support
from .mutation_samples import DOCUMENT
from .test_mutation import healthy

verify = support.load_verify()
ROOT = support.VERIFY_CODE.parents[1]
KNOWN = "`Core.Boundary.Text`, `Core.Platform.Logging`, `language_service.auth`"


class MutationModuleTests(fixture.MutationFixture):
    def test_a_core_module_runs_stryker_and_not_mutmut(self) -> None:
        runner = healthy()

        result = self.run_step(runner, "Core.Platform.Logging")

        self.assertTrue(result.ok)
        self.assertEqual(
            (len(runner.called("stryker")), len(runner.called("--max-children"))), (1, 0)
        )

    def test_a_service_module_runs_mutmut_for_it_alone_and_not_stryker(self) -> None:
        runner = healthy()

        result = self.run_step(runner, "language_service.auth")

        self.assertTrue(result.ok)
        self.assertEqual(
            (len(runner.called("stryker")), len(runner.called("--max-children"))), (0, 1)
        )
        self.assertFalse(any("logging_" in line for line in result.details))

    def test_the_scope_names_the_module_and_how_many_there_are(self) -> None:
        result = self.run_step(healthy(), "Core.Platform.Logging")

        expected = "módulo `Core.Platform.Logging` (coluna Crítica, 4 no total); limite"
        self.assertTrue(result.details[0].startswith(expected), result.details[0])

    def test_the_limits_are_per_module(self) -> None:
        result = self.run_step(healthy(), "Core.Platform.Logging")

        self.assertTrue(result.details[0].endswith("20 % de sobreviventes e 20 min por módulo"))

    def test_a_module_that_is_not_critical_fails_naming_the_critical_ones(self) -> None:
        runner = healthy()

        result = self.run_step(runner, "Core.Platform")

        self.assertFalse(result.ok)
        self.assertEqual(runner.commands, [])
        self.assertIn("`Core.Platform` não é módulo crítico", result.details[0])
        self.assertTrue(result.details[0].endswith(f"são: {KNOWN}, `language_service.logging_`"))

    def test_the_areas_document_is_read_once_per_run(self) -> None:
        with mock.patch.object(mutation, "read_areas", wraps=areas_doc.read_areas) as read:
            self.run_step(healthy(), "Core.Platform.Logging")

        self.assertEqual(read.call_count, 1)

    def test_without_a_module_every_critical_module_runs(self) -> None:
        runner = healthy()

        self.run_step(runner)

        self.assertEqual(
            (len(runner.called("stryker")), len(runner.called("--max-children"))), (1, 2)
        )

    def test_the_names_of_the_matrix_follow_the_critical_field_by_file(self) -> None:
        names, problems = mutation.names(self.root)

        self.assertEqual(problems, ())
        self.assertEqual(
            names,
            (
                "Core.Boundary.Text",
                "Core.Platform.Logging",
                "language_service.auth",
                "language_service.logging_",
            ),
        )

    def test_a_document_with_no_critical_module_gives_no_names_and_a_problem(self) -> None:
        document = DOCUMENT
        support.write_areas(self.root, document.replace("| sim |", "| não |"))

        names, problems = mutation.names(self.root)

        self.assertEqual((names, len(problems)), ((), 1))


class MutationModulesCommandTests(unittest.TestCase):
    def test_the_command_prints_the_critical_modules_of_the_document_as_json(self) -> None:
        buffer = io.StringIO()
        with contextlib.redirect_stdout(buffer):
            code = verify.main(["--mutation-modules"])

        listed = [a.prefix for a in areas_doc.read_areas(ROOT).areas if a.critical]
        self.assertEqual((code, json.loads(buffer.getvalue())), (0, listed))
