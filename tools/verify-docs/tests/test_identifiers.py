"""Tests for the identifiers check: a code name cited in a document is declared in the code.

Each test builds a throwaway repository with synthetic code and documents in a temporary
directory and points the verifier at it with --root.
"""

import tempfile
import unittest
from pathlib import Path

from .test_verify import VALID_DOC, run_verifier

CSHARP = (
    "namespace Sample.Reports;\n\n"
    "public sealed class ReportWriter\n{\n"
    "    public const int MaxRows = 10;\n"
    "    public string OutputName { get; } = string.Empty;\n"
    "    public void WriteSummary(int rows) { }\n}\n\n"
    "public interface IReportSink\n{\n    void PushLine(string line);\n}\n\n"
    "public sealed record ReportStatus(bool IsReady, int RowCount = 0);\n\n"
    "public enum ReportKind\n{\n    PlainText,\n    RichText = 2,\n}\n"
)

PYTHON = (
    "from dataclasses import dataclass\n\n"
    "default_folder = 'reports'\n\n\n"
    "@dataclass(frozen=True)\n"
    "class ReportOptions:\n    page_size: int\n\n\n"
    "class ReportQueue:\n    def __init__(self) -> None:\n        self.pending_count = 0\n\n\n"
    "async def build_report(rows: int) -> str:\n    return str(rows)\n"
)


class IdentifiersTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)
        self.write("core/src/Sample.Reports/ReportWriter.cs", CSHARP)
        self.write("language-service/src/report_service/__init__.py", "")
        self.write("language-service/src/report_service/report_queue.py", PYTHON)

    def write(self, relative: str, content: str) -> None:
        target = self.root / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(content, encoding="utf-8", newline="\n")

    def verify_citing(self, *citations: str) -> tuple[int, str]:
        text = " ".join(f"`{citation}`" for citation in citations)
        self.write("docs/001-cites.md", VALID_DOC + f"\nCita {text}.\n")
        return run_verifier(self.root, "--only", "identifiers")

    def test_identifier_missing_from_the_code_fails_with_document_line_and_name(self) -> None:
        code, output = self.verify_citing("ReportReader")

        self.assertEqual(code, 1, output)
        self.assertIn("docs/001-cites.md:7  [identifiers] `ReportReader` não é declarado", output)

    def test_csharp_types_and_members_pass(self) -> None:
        code, output = self.verify_citing(
            "ReportWriter", "IReportSink", "MaxRows", "OutputName", "WriteSummary()", "PushLine"
        )

        self.assertEqual(code, 0, output)

    def test_record_positional_properties_and_enum_members_pass(self) -> None:
        code, output = self.verify_citing("ReportStatus", "IsReady", "RowCount", "PlainText")

        self.assertEqual(code, 0, output)

    def test_python_functions_classes_attributes_and_modules_pass(self) -> None:
        code, output = self.verify_citing(
            "build_report",
            "ReportOptions",
            "page_size",
            "pending_count",
            "default_folder",
            "report_queue",
            "report_service",
        )

        self.assertEqual(code, 0, output)

    def test_identifier_does_not_need_its_file_path_cited(self) -> None:
        code, output = self.verify_citing("ReportQueue")

        self.assertEqual(code, 0, output)

    def test_qualified_name_with_declared_type_and_member_passes(self) -> None:
        code, output = self.verify_citing("ReportWriter.WriteSummary", "ReportKind.RichText")

        self.assertEqual(code, 0, output)

    def test_qualified_name_with_a_missing_member_fails(self) -> None:
        code, output = self.verify_citing("ReportWriter.MissingMember")

        self.assertEqual(code, 1, output)
        self.assertIn("`ReportWriter.MissingMember` não é declarado", output)

    def test_module_qualified_name_with_a_missing_member_fails(self) -> None:
        code, output = self.verify_citing("report_service.missing_module")

        self.assertEqual(code, 1, output)

    def test_namespace_qualified_by_a_single_word_is_not_checked(self) -> None:
        code, output = self.verify_citing("Sample.Reports", "Core.MissingProject")

        self.assertEqual(code, 0, output)

    def test_qualified_name_with_a_missing_type_fails(self) -> None:
        code, output = self.verify_citing("ReportReader.WriteSummary")

        self.assertEqual(code, 1, output)
        self.assertIn("`ReportReader.WriteSummary` não é declarado", output)

    def test_missing_interface_with_leading_acronym_fails(self) -> None:
        code, output = self.verify_citing("IReporter")

        self.assertEqual(code, 1, output)

    def test_missing_camel_case_name_fails(self) -> None:
        code, output = self.verify_citing("writeReport")

        self.assertEqual(code, 1, output)

    def test_missing_snake_case_name_fails(self) -> None:
        code, output = self.verify_citing("build_summary")

        self.assertEqual(code, 1, output)

    def test_camel_case_wire_name_of_a_declared_property_passes(self) -> None:
        code, output = self.verify_citing("outputName")

        self.assertEqual(code, 0, output)

    def test_identifier_inside_fenced_code_block_is_not_checked(self) -> None:
        self.write("docs/001-fence.md", VALID_DOC + "\n```\nCita `ReportReader`.\n```\n")

        code, output = run_verifier(self.root, "--only", "identifiers")

        self.assertEqual(code, 0, output)

    def test_every_document_citing_a_renamed_identifier_is_reported(self) -> None:
        self.write("docs/001-first.md", VALID_DOC + "\nCita `ReportPrinter`.\n")
        self.write("docs/sub/002-second.md", VALID_DOC + "\nCita `ReportPrinter`.\n")

        code, output = run_verifier(self.root, "--only", "identifiers")

        self.assertEqual(code, 1, output)
        self.assertIn("docs/001-first.md:7", output)
        self.assertIn("docs/sub/002-second.md:7", output)
        self.assertIn("identifiers: 2", output)

    def test_words_constants_commands_keys_and_files_are_not_checked(self) -> None:
        code, output = self.verify_citing(
            "Justification", "PESSOA_1", "--step build_core", "Host:LogFile", "report_tool.toml"
        )

        self.assertEqual(code, 0, output)

    def test_platform_name_cited_by_the_documents_passes(self) -> None:
        code, output = self.verify_citing("ResourceManager")

        self.assertEqual(code, 0, output)

    def test_name_declared_only_in_build_output_fails(self) -> None:
        self.write("core/src/Sample.Reports/obj/Generated.cs", "public class ReportReader { }\n")

        code, output = self.verify_citing("ReportReader")

        self.assertEqual(code, 1, output)

    def test_name_declared_only_in_a_fixture_fails(self) -> None:
        self.write("core/tests/fixtures/sample/Generated.cs", "public class ReportReader { }\n")

        code, output = self.verify_citing("ReportReader")

        self.assertEqual(code, 1, output)

    def test_name_declared_only_in_a_virtual_environment_fails(self) -> None:
        self.write("language-service/.venv/lib/report_reader.py", "def read_rows(): ...\n")

        code, output = self.verify_citing("read_rows")

        self.assertEqual(code, 1, output)

    def test_name_declared_in_tools_passes(self) -> None:
        self.write("tools/verify-code/steps/build_report_step.py", "NAME = 'step'\n")

        code, output = self.verify_citing("build_report_step")

        self.assertEqual(code, 0, output)


if __name__ == "__main__":
    unittest.main()
