"""Tells if a commit of the main holds a tree a green pull request run already tested.

Each pull request run records the tree of the merge ref it checked out, in an artifact named after
that tree. The push is skipped only for a tree such a run recorded, when the run is a finished green
run of the workflow and every check the ruleset requires is green on its head. Nothing is deduced
from the head or the base. Any doubt or error returns nothing: the whole ruler runs.
"""

from __future__ import annotations

import json
from dataclasses import dataclass
from typing import Any, cast

from steps.common import Context

WORKFLOW = "verify-code.yml"
JOBS = {"dotnet": "Núcleo (.NET) e ferramentas", "python": "Serviço de linguagem (Python)"}
ARTIFACT = "cobertura-pr-{language}-{head}"
TREE_ARTIFACT = "arvore-{tree}"
GREEN = ("success", "skipped")
Json = dict[str, Any]


@dataclass(frozen=True)
class Tested:
    run: int
    head: str
    promote: tuple[str, ...]


def _api(ctx: Context, path: str) -> Any:
    outcome = ctx.run(["gh", "api", path], ctx.root)
    return json.loads(outcome.text) if outcome.ok else None


def _object(ctx: Context, path: str) -> Json:
    return cast("Json", _api(ctx, path) or {})


def _required(ctx: Context, repository: str) -> set[str]:
    rules = cast("list[Json]", _api(ctx, f"repos/{repository}/rules/branches/main") or [])
    return {
        str(c["context"])
        for rule in rules
        if rule["type"] == "required_status_checks"
        for c in cast("list[Json]", rule["parameters"]["required_status_checks"])
    }


def _all_required_green(ctx: Context, repository: str, head: str, required: set[str]) -> bool:
    checks = _object(ctx, f"repos/{repository}/commits/{head}/check-runs?per_page=100")
    runs = cast("list[Json]", checks.get("check_runs", []))
    states = {str(c["name"]): c["conclusion"] for c in runs if c["name"] in required}
    return bool(required) and set(states) == required and all(v in GREEN for v in states.values())


def _promotable(ctx: Context, repository: str, run: int, head: str) -> tuple[str, ...] | None:
    """The languages whose heavy job ran in the run, or None if a table is missing."""
    base = f"repos/{repository}/actions/runs/{run}"
    jobs, kept = _api(ctx, f"{base}/jobs?per_page=100"), _api(ctx, f"{base}/artifacts?per_page=100")
    if jobs is None or kept is None:
        return None
    done = {
        str(j["name"]) for j in cast("list[Json]", jobs["jobs"]) if j["conclusion"] == "success"
    }
    names = {str(a["name"]) for a in cast("list[Json]", kept["artifacts"]) if not a["expired"]}
    ran = tuple(n for n, title in JOBS.items() if title in done)
    return ran if all(ARTIFACT.format(language=n, head=head) in names for n in ran) else None


def _recorded_by(ctx: Context, repository: str, tree: str) -> list[Json]:
    name = TREE_ARTIFACT.format(tree=tree)
    found = _object(ctx, f"repos/{repository}/actions/artifacts?name={name}&per_page=100")
    runs: list[Json] = []
    for artifact in cast("list[Json]", found.get("artifacts", [])):
        run = _object(ctx, f"repos/{repository}/actions/runs/{artifact['workflow_run']['id']}")
        done = run.get("event") == "pull_request" and run.get("conclusion") == "success"
        if done and not artifact["expired"] and WORKFLOW in str(run.get("path")):
            runs.append(run)
    return runs


def tested(ctx: Context, repository: str, sha: str) -> Tested | None:
    outcome = ctx.run(["git", "rev-parse", f"{sha}^{{tree}}"], ctx.root)
    required = _required(ctx, repository)
    for run in _recorded_by(ctx, repository, outcome.text.strip()) if outcome.ok else []:
        head = str(run["head_sha"])
        promote = _promotable(ctx, repository, int(run["id"]), head)
        if promote is not None and _all_required_green(ctx, repository, head, required):
            return Tested(int(run["id"]), head, promote)
    return None
