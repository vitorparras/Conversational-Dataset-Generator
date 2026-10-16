"""Tests for the reader of accepted-risks.toml: entries, deadlines and the problems it names."""

from __future__ import annotations

import tempfile
import unittest
from datetime import date
from pathlib import Path

from steps import accepted_risks
from steps.accepted_risk import Risk
from steps.risk_registry import Registry

ENTRY = (
    "[[risk]]\n"
    'id = "CVE-0000-0001"\n'
    'subject = "Zqx.Package"\n'
    "expires = 2026-12-31\n"
    'reason = "sem versão corrigida"\n'
)
RISK = Risk("CVE-0000-0001", "Zqx.Package", date(2026, 12, 31), "sem versão corrigida")
SHIPPED = Path(__file__).resolve().parents[1] / "accepted-risks.toml"


class ParseTests(unittest.TestCase):
    def test_a_file_with_only_comments_has_no_risks_and_no_problems(self) -> None:
        registry = accepted_risks.parse("# nada aceito\n")

        self.assertEqual(registry, Registry())

    def test_a_table_becomes_a_risk_with_its_deadline(self) -> None:
        registry = accepted_risks.parse(ENTRY)

        self.assertEqual(registry.problems, ())
        self.assertEqual(registry.risks, (RISK,))

    def test_a_table_with_a_missing_field_is_a_problem_naming_its_position(self) -> None:
        registry = accepted_risks.parse(ENTRY + '\n[[risk]]\nid = "CVE-0000-0002"\n')

        self.assertEqual(registry.risks, (RISK,))
        self.assertIn(
            "[[risk]] n.º 2: campos ausentes ['subject', 'expires', 'reason']",
            registry.problems[0],
        )

    def test_a_date_written_as_text_is_a_problem(self) -> None:
        registry = accepted_risks.parse(ENTRY.replace("2026-12-31", '"2026-12-31"'))

        self.assertIn("expires deve ser data TOML exata", registry.problems[0])

    def test_a_date_with_a_time_is_a_problem_and_not_a_crash(self) -> None:
        registry = accepted_risks.parse(ENTRY.replace("2026-12-31", "2026-12-31T10:00:00"))

        self.assertIn("expires deve ser data TOML exata", registry.problems[0])

    def test_a_blank_id_is_a_problem(self) -> None:
        registry = accepted_risks.parse(ENTRY.replace('"CVE-0000-0001"', '"  "'))

        self.assertIn("devem ser texto não vazio", registry.problems[0])

    def test_a_blank_reason_is_a_problem(self) -> None:
        registry = accepted_risks.parse(ENTRY.replace('"sem versão corrigida"', '""'))

        self.assertIn("devem ser texto não vazio", registry.problems[0])

    def test_an_unknown_field_is_a_problem(self) -> None:
        registry = accepted_risks.parse(ENTRY + 'owner = "someone"\n')

        self.assertIn("desconhecidos ['owner']", registry.problems[0])

    def test_a_risk_key_that_is_not_a_list_of_tables_is_a_problem(self) -> None:
        registry = accepted_risks.parse("risk = 1\n")

        self.assertIn("'risk' deve ser uma lista de tabelas", registry.problems[0])

    def test_a_single_table_instead_of_an_array_is_a_problem(self) -> None:
        registry = accepted_risks.parse(ENTRY.replace("[[risk]]", "[risk]"))

        self.assertIn("'risk' deve ser uma lista de tabelas", registry.problems[0])

    def test_an_unknown_top_level_key_is_a_problem_even_if_it_looks_like_a_typo(self) -> None:
        registry = accepted_risks.parse(ENTRY.replace("[[risk]]", "[[risks]]"))

        self.assertEqual(
            registry.problems,
            ("tools/verify-code/accepted-risks.toml: chave de topo desconhecida 'risks'",),
        )

    def test_malformed_toml_is_a_problem_naming_the_line(self) -> None:
        registry = accepted_risks.parse(ENTRY + "expires = = 1\n")

        self.assertIn("malformado", registry.problems[0])
        self.assertIn("line 6", registry.problems[0])

    def test_the_shipped_file_is_valid_and_accepts_nothing(self) -> None:
        registry = accepted_risks.load(SHIPPED)

        self.assertEqual(registry, Registry())


class LoadAndMatchTests(unittest.TestCase):
    def test_a_missing_file_is_a_problem(self) -> None:
        with tempfile.TemporaryDirectory() as folder:
            registry = accepted_risks.load(Path(folder) / "none.toml")

        self.assertIn("não existe", registry.problems[0])

    def test_a_risk_is_not_expired_on_its_last_day(self) -> None:
        self.assertEqual(accepted_risks.expired((RISK,), date(2026, 12, 31)), ())

    def test_a_risk_is_expired_the_day_after(self) -> None:
        self.assertEqual(accepted_risks.expired((RISK,), date(2027, 1, 1)), (RISK,))

    def test_a_risk_covers_its_own_id_and_subject(self) -> None:
        self.assertTrue(accepted_risks.accepts((RISK,), "CVE-0000-0001", "Zqx.Package"))

    def test_a_risk_does_not_cover_another_subject(self) -> None:
        self.assertFalse(accepted_risks.accepts((RISK,), "CVE-0000-0001", "Other.Package"))

    def test_a_risk_does_not_cover_another_id(self) -> None:
        self.assertFalse(accepted_risks.accepts((RISK,), "CVE-0000-0009", "Zqx.Package"))
