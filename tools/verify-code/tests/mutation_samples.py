"""Synthetic samples of the mutation tests: the document section, tool reports and a diff."""

from __future__ import annotations

import json

from steps import areas_doc

POLICY = (
    "# 017\n\n## Regras\n\n"
    "O teste de mutação roda sobre as áreas com `sim` em **Crítica**, que são a lista de módulos. "
    "Por\nmódulo, no máximo **20 % dos mutantes gerados** podem sobreviver. Teto de tempo: "
    "**20 minutos por\nmódulo** dentro da verificação. No núcleo, a verificação não conta\n"
    "**o mutante `false`→`true` do argumento de `ConfigureAwait`**.\n\n## Depois\n"
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


AWAITED = "        await ReleaseAsync(connection).ConfigureAwait(false);\n"


def stryker_span(
    name: str, replacement: str, span: tuple[int, int, int], status: str
) -> dict[str, object]:
    """A Stryker mutant of one line; ``span`` is (line, first column, end column), from 1."""
    line = span[0]
    start, end = ({"line": line, "column": column} for column in span[1:])
    location = {"start": start, "end": end}
    return {
        "id": f"{line}-{span[1]}",
        "mutatorName": name,
        "replacement": replacement,
        "location": location,
        "status": status,
    }


def stryker_with_source(file: str, source: str, *mutants: dict[str, object]) -> str:
    """A report that carries the source of its file, as Stryker's JSON reporter does."""
    files = {file: {"source": source, "mutants": mutants}}
    return json.dumps({"schemaVersion": "2", "files": files})


def awaited_pair(status: str) -> tuple[dict[str, object], dict[str, object]]:
    """The Statement mutation of ``AWAITED`` and the Boolean one of its `false` argument."""
    column = AWAITED.index("false") + 1
    statement = stryker_span("Statement mutation", ";", (1, 9, len(AWAITED)), status)
    argument = stryker_span("Boolean mutation", "true", (1, column, column + 5), status)
    return statement, argument
