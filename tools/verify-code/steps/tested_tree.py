"""Tells if a commit of the main holds a tree a green pull request run already tested.

Any doubt or error returns nothing, and the whole ruler runs as before. It takes: one parent and one
merged pull request; a head that contains the parent and has the tree of the commit; a green newest
finished run on that head; and, for each heavy job that ran, its coverage table, still kept.
"""

from __future__ import annotations

import json
from dataclasses import dataclass
from typing import Any, cast

from steps.common import Context

WORKFLOW = "verify-code.yml"
JOBS = {"dotnet": "Núcleo (.NET) e ferramentas", "python": "Serviço de linguagem (Python)"}
ARTIFACT = "cobertura-pr-{language}-{head}"
ONE_PARENT = 2
Json = dict[str, Any]


@dataclass(frozen=True)
class Tested:
    """The green run and head that tested the tree, and the languages whose table to promote."""

    run: int
    head: str
    promote: tuple[str, ...]


def _api(ctx: Context, path: str) -> Json | None:
    outcome = ctx.run(["gh", "api", path], ctx.root)
    return cast("Json", json.loads(outcome.text)) if outcome.ok else None


def _git(ctx: Context, *args: str) -> str | None:
    outcome = ctx.run(["git", *args], ctx.root)
    return outcome.text.strip() if outcome.ok else None


def _merged_pull(ctx: Context, repository: str, sha: str) -> Json | None:
    outcome = ctx.run(["gh", "api", f"repos/{repository}/commits/{sha}/pulls"], ctx.root)
    pulls = cast("list[Json]", json.loads(outcome.text)) if outcome.ok else []
    merged = [p for p in pulls if p.get("merge_commit_sha") == sha and p.get("merged_at")]
    return merged[0] if len(merged) == 1 else None


def _same_tree(ctx: Context, sha: str, head: str) -> bool:
    """True when ``head`` contains the parent of ``sha`` and has the tree of ``sha``."""
    parent, tree = _git(ctx, "rev-parse", f"{sha}^"), _git(ctx, "rev-parse", f"{sha}^{{tree}}")
    return (
        parent is not None
        and tree is not None
        and _git(ctx, "rev-parse", f"{head}^{{tree}}") == tree
        and _git(ctx, "merge-base", "--is-ancestor", parent, head) is not None
    )


def _green_run(ctx: Context, repository: str, head: str) -> Json | None:
    path = (
        f"repos/{repository}/actions/workflows/{WORKFLOW}/runs?head_sha={head}&event=pull_request"
    )
    runs = cast("list[Json]", (_api(ctx, path) or {}).get("workflow_runs", []))
    done = [r for r in runs if r["status"] == "completed"]
    return done[0] if done and done[0]["conclusion"] == "success" else None


def _promotable(ctx: Context, repository: str, run: int, head: str) -> tuple[str, ...] | None:
    """The languages whose heavy job ran in the run, or None if a table is missing."""
    base = f"repos/{repository}/actions/runs/{run}"
    jobs, artifacts = (
        _api(ctx, f"{base}/jobs?per_page=100"),
        _api(ctx, f"{base}/artifacts?per_page=100"),
    )
    if jobs is None or artifacts is None:
        return None
    done = {
        str(j["name"]) for j in cast("list[Json]", jobs["jobs"]) if j["conclusion"] == "success"
    }
    kept = {str(a["name"]) for a in cast("list[Json]", artifacts["artifacts"]) if not a["expired"]}
    ran = tuple(n for n, title in JOBS.items() if title in done)
    return ran if all(ARTIFACT.format(language=n, head=head) in kept for n in ran) else None


def tested(ctx: Context, repository: str, sha: str) -> Tested | None:
    parents = (_git(ctx, "rev-list", "--parents", "-n", "1", sha) or "").split()
    pull = _merged_pull(ctx, repository, sha) if len(parents) == ONE_PARENT else None
    if pull is None:
        return None
    head = str(pull["head"]["sha"])
    if _git(ctx, "fetch", "-q", "origin", f"refs/pull/{pull['number']}/head") is None:
        return None
    green = _green_run(ctx, repository, head) if _same_tree(ctx, sha, head) else None
    promote = _promotable(ctx, repository, int(green["id"]), head) if green else None
    return None if promote is None or green is None else Tested(int(green["id"]), head, promote)
