"""Reads accepted-risks.toml: findings accepted on purpose, each with a deadline and a reason."""

from __future__ import annotations

import tomllib
from datetime import date
from pathlib import Path
from typing import Any, cast

from steps.accepted_risk import Risk
from steps.common import StepResult, failed
from steps.risk_registry import Registry

FILE = "tools/verify-code/accepted-risks.toml"
TABLE = "risk"
TEXT_FIELDS = ("id", "subject", "reason")
FIELDS = ("id", "subject", "expires", "reason")


def to_risk(entry: dict[str, Any], position: int) -> Risk | str:
    """The risk of one `[[risk]]` table, or the problem that makes it unusable."""
    where = f"{FILE}: [[{TABLE}]] n.º {position}"
    unknown = [key for key in entry if key not in FIELDS]
    missing = [name for name in FIELDS if name not in entry]
    if unknown or missing:
        return f"{where}: campos ausentes {missing}, desconhecidos {unknown}"
    texts = [entry[name] for name in TEXT_FIELDS]
    if type(entry["expires"]) is not date:
        return f"{where}: expires deve ser data TOML exata (AAAA-MM-DD), sem hora"
    if not all(isinstance(text, str) and text.strip() for text in texts):
        return f"{where}: id, subject e reason devem ser texto não vazio"
    return Risk(entry["id"], entry["subject"], entry["expires"], entry["reason"])


def is_table_list(value: object) -> bool:
    """True for a list whose items are all tables."""
    return isinstance(value, list) and all(
        isinstance(item, dict) for item in cast("list[object]", value)
    )


def _entries_of(data: dict[str, Any]) -> tuple[list[dict[str, Any]], list[str]]:
    """The `[[risk]]` tables and the problems with the file's top level."""
    problems = [f"{FILE}: chave de topo desconhecida '{key}'" for key in data if key != TABLE]
    entries: object = data.get(TABLE, [])
    if not is_table_list(entries):
        return [], [*problems, f"{FILE}: '{TABLE}' deve ser uma lista de tabelas [[{TABLE}]]"]
    return cast("list[dict[str, Any]]", entries), problems


def parse(text: str) -> Registry:
    try:
        data = tomllib.loads(text)
    except tomllib.TOMLDecodeError as error:
        return Registry(problems=(f"{FILE} malformado: {error}",))
    entries, problems = _entries_of(data)
    converted = [to_risk(entry, number) for number, entry in enumerate(entries, start=1)]
    risks = tuple(item for item in converted if isinstance(item, Risk))
    return Registry(risks, (*problems, *(item for item in converted if isinstance(item, str))))


def load(path: Path) -> Registry:
    if not path.is_file():
        return Registry(problems=(f"{FILE} não existe: crie-o, vazio basta um arquivo em branco",))
    return parse(path.read_text(encoding="utf-8"))


def expired(risks: tuple[Risk, ...], today: date) -> tuple[Risk, ...]:
    return tuple(risk for risk in risks if risk.expires < today)


def covering(risks: tuple[Risk, ...], ids: tuple[str, ...], subject: str) -> Risk | None:
    """The risk that accepts a finding known by any of ``ids`` on ``subject``, if there is one."""
    return next((risk for risk in risks if risk.id in ids and risk.subject == subject), None)


def accepted_line(risk: Risk) -> str:
    return f"  - risco aceito até {risk.expires}: {risk.id} em {risk.subject} ({risk.reason})"


def check(root: Path, today: date, step: str) -> Registry | StepResult:
    """The registry when it is readable and in date; otherwise the failed result of ``step``."""
    registry = load(root / FILE)
    late = [
        f"risco aceito venceu em {r.expires}: {r.id} em {r.subject}"
        for r in expired(registry.risks, today)
    ]
    if registry.problems or late:
        return failed(step, *registry.problems, *late)
    return registry
