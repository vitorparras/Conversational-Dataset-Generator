"""Audits both dependency trees, the core's and the language service's, transitive included.

A known vulnerability fails unless accepted-risks.toml accepts it within its deadline. A tree that
cannot be audited fails too: missing lock, failed export, unreachable vulnerability source. The
check needs the network, or a recent vulnerability database in cache, and never passes unaudited."""

from __future__ import annotations

from datetime import date

from steps import accepted_risks, core_audit, service_audit
from steps.common import Context, StepResult

NAME = "dependencies"


def audit(ctx: Context, today: date) -> StepResult:
    """Both trees are audited even when the first fails, so one run names every finding."""
    registry = accepted_risks.check(ctx.root, today, NAME)
    if isinstance(registry, StepResult):
        return registry
    parts = (core_audit.audit(ctx, registry.risks), service_audit.audit(ctx, registry.risks))
    details = tuple(line for part in parts for line in part.details)
    return StepResult(NAME, all(part.ok for part in parts), details)


def run(ctx: Context) -> StepResult:
    return audit(ctx, date.today())
