"""Tests for steps/properties.py: both languages must have run properties, each citing its source."""

import importlib.util
import unittest
from pathlib import Path

_spec = importlib.util.spec_from_file_location("verify_code_support", Path(__file__).resolve().parent / "_support.py")
support = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(support)

from steps import properties  # noqa: E402


class PropertiesStepTests(support.RepositoryTestCase):
    def setUp(self) -> None:
        super().setUp()
        (self.out_dir / "core").mkdir(parents=True, exist_ok=True)
        (self.out_dir / "core" / "results.trx").write_bytes((support.FIXTURES / "results.trx").read_bytes())
        (self.out_dir / "service" / "junit.xml").write_bytes((support.FIXTURES / "junit.xml").read_bytes())
        self.write("core/tests/Core.Tests.Properties/Sample.cs", "// Requirement 032 says so.\n")
        self.write("language-service/tests/properties/test_sample.py", "# Decision 0018 says so.\n")

    def test_both_languages_with_a_cited_property_pass(self) -> None:
        result = properties.run(self.context())
        self.assertTrue(result.ok, result.lines)

    def test_a_side_that_executed_no_property_fails(self) -> None:
        """Issue 41 CA-5: the side without an arrangement fails instead of passing empty."""
        (self.out_dir / "service" / "junit.xml").unlink()
        result = properties.run(self.context())
        self.assertFalse(result.ok)
        self.assertIn("serviço", " ".join(result.lines))

    def test_a_core_side_whose_only_property_was_skipped_fails(self) -> None:
        trx = self.out_dir / "core" / "results.trx"
        trx.write_text(trx.read_text(encoding="utf-8").replace('outcome="Passed" />\n    <UnitTestResult executionId="e2"', 'outcome="NotExecuted" />\n    <UnitTestResult executionId="e2"', 1), encoding="utf-8")
        result = properties.run(self.context())
        self.assertFalse(result.ok)
        self.assertIn("núcleo", " ".join(result.lines))

    def test_a_property_file_that_cites_no_document_fails_naming_the_file(self) -> None:
        """Issue 41 CA-3: the property comes from a written invariant, not from the code it checks."""
        self.write("core/tests/Core.Tests.Properties/Describes.cs", "// Checks that the code returns what it returns.\n")
        result = properties.run(self.context())
        self.assertFalse(result.ok)
        self.assertIn("Describes.cs", " ".join(result.lines))


if __name__ == "__main__":
    unittest.main()
