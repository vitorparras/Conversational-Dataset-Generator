"""Synthetic samples of the mutation tests: the document section, tool reports and a diff."""

from __future__ import annotations

import json

from steps import areas_doc

POLICY = (
    "# 017\n\n## Regras\n\n"
    "O teste de mutação roda sobre as áreas com `sim` em **Crítica**, que são a lista de módulos. "
    "Por\nmódulo, no máximo **20 % dos mutantes gerados** podem sobreviver. Teto de tempo: "
    "**20 minutos por\nlinguagem** dentro da verificação.\n\n## Depois\n"
)
DOCUMENT = "\n".join(
    (
        "# 014",
        areas_doc.HEADER,
        "|---|---|---|---|---|",
        "| Registro do núcleo | `Core.Platform.Logging` | 90 % | sim | crítica |",
        "| Plataforma | `Core.Platform` | 80 % | não | demais pastas |",
        "| Texto | `Core.Boundary.Text` | 90 % | sim | ainda sem código |",
        "| Autenticação | `language_service.auth` | 90 % | sim | segredo |",
        "| Registro do serviço | `language_service.logging_` | 90 % | sim | registro |",
        "| Serviço | `language_service` | 80 % | não | demais módulos |",
        "",
    )
)
AUTH_TEST = "from language_service.auth import Verifier\n"
LOGGING_TEST = "from language_service import logging_\n"
SOURCE = "class Verifier:\n    def accepts(self, given):\n        return given == self.secret\n"
SHOWN = (
    "# language_service.auth.xǁVerifierǁaccepts__mutmut_1: survived\n"
    "--- src/language_service/auth.py\n+++ src/language_service/auth.py\n@@ -1,2 +1,2 @@\n"
    " def accepts(self, given):\n-    return given == self.secret\n+    return given != self.secret"
)


def stryker_mutant(line: int, status: str) -> dict[str, object]:
    location = {"start": {"line": line, "column": 9}, "end": {"line": line, "column": 15}}
    replacement = {"location": location, "replacement": "a != b"}
    return {"id": str(line), "mutatorName": "Equality mutation", "status": status, **replacement}


def stryker_report(file: str, *statuses: str) -> str:
    """A report whose file holds one mutant per status, on lines numbered from 10."""
    mutants = [stryker_mutant(10 + n, status) for n, status in enumerate(statuses)]
    return json.dumps({"schemaVersion": "2", "files": {file: {"mutants": mutants}}})


def meta(codes: dict[str, int | None]) -> str:
    return json.dumps({"exit_code_by_key": codes})
