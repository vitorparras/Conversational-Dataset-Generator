"""Audits the dependency tree of the language service, transitive included.

A known vulnerability fails unless accepted-risks.toml accepts it within its deadline. A tree that
cannot be audited fails too: missing lock, failed export, unreachable vulnerability source. The
check needs the network, or a recent vulnerability database in cache, and never passes unaudited."""

from __future__ import annotations

from datetime import date

from steps import accepted_risks, service_audit
from steps.common import Context, StepResult

NAME = "dependencies"


def audit(ctx: Context, today: date) -> StepResult:
    registry = accepted_risks.check(ctx.root, today, NAME)
    if isinstance(registry, StepResult):
        return registry
    service = service_audit.audit(ctx, registry.risks)
    return StepResult(NAME, service.ok, service.details)


def run(ctx: Context) -> StepResult:
    return audit(ctx, date.today())
