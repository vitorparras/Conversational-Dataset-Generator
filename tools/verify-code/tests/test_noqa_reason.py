"""Tests for the reason a `# noqa` of a ruff security rule must carry in the language service."""

from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from steps import lint_service, noqa_reason
from steps.common import StepResult

from . import support

ROOT = support.VERIFY_CODE.parents[1]
SOURCE = "language-service/tests/unit/test_zqx.py"
CALL = "subprocess.run(command)"


class NoqaReasonTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)
        (self.root / lint_service.VENV).mkdir(parents=True)

    def lint_with(self, line: str, source: str = SOURCE) -> StepResult:
        support.write(self.root, source, f"x = 1\n{line}\n")
        return lint_service.run(support.context(self.root, support.FakeRunner()))

    def test_a_security_noqa_without_reason_fails_naming_file_and_line(self) -> None:
        found = self.lint_with(f"{CALL}  # noqa: S603")

        self.assertFalse(found.ok)
        self.assertIn(
            f"{SOURCE}:2: `# noqa` de regra S sem motivo em pelo menos três palavras", found.details
        )

    def test_a_reason_of_two_words_is_not_enough(self) -> None:
        found = self.lint_with(f"{CALL}  # noqa: S603 fixed command")

        self.assertFalse(found.ok)

    def test_a_security_code_among_others_still_needs_a_reason(self) -> None:
        found = self.lint_with(f"{CALL}  # noqa: E501, S603")

        self.assertFalse(found.ok)

    def test_a_security_noqa_with_a_reason_lets_the_linters_run(self) -> None:
        found = self.lint_with(f"{CALL}  # noqa: S603 starts the service from a fixed command")

        self.assertTrue(found.ok, found.details)

    def test_a_noqa_of_other_rules_needs_no_reason(self) -> None:
        found = self.lint_with(f"{CALL}  # noqa: SIM115, E501")

        self.assertTrue(found.ok, found.details)

    def test_the_sources_of_the_service_are_checked_too(self) -> None:
        found = self.lint_with(f"{CALL}  # noqa: S603", "language-service/src/zqx/run.py")

        self.assertFalse(found.ok)

    def test_the_virtual_environment_is_not_checked(self) -> None:
        found = self.lint_with(f"{CALL}  # noqa: S603", f"{lint_service.VENV}/lib/zqx.py")

        self.assertTrue(found.ok, found.details)

    def test_the_service_of_the_repository_explains_every_security_noqa(self) -> None:
        self.assertEqual(noqa_reason.unexplained(ROOT, lint_service.SERVICE), [])


class RuffConfigurationTests(unittest.TestCase):
    def setUp(self) -> None:
        self.text = (ROOT / "ruff.toml").read_text(encoding="utf-8")

    def test_the_security_rules_and_named_noqa_are_selected(self) -> None:
        self.assertRegex(self.text, r'select = \[.*"S", "PGH004"\]')

    def test_only_the_tools_skip_the_security_rules(self) -> None:
        self.assertIn('"tools/**" = ["S"]', self.text)
        self.assertEqual(self.text.count('= ["S"]'), 1)

    def test_the_service_tests_skip_only_the_assert_rule(self) -> None:
        self.assertIn('"language-service/tests/**" = ["S101"]', self.text)


if __name__ == "__main__":
    unittest.main()
