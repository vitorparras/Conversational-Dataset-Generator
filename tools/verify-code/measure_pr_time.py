#!/usr/bin/env python3
"""Measures where the time of the last merged pull requests goes, from what GitHub keeps.

Read-only: it calls `gh api` and prints a markdown report, so the comment on the issue can be
reproduced with `python tools/verify-code/measure_pr_time.py --limit 20`. For each merged pull
request it reads the runs of the pull-request event inside its lifetime, the jobs of every
attempt, the commits and the comments, and reports:

* the time of each job (started to completed), per pull request and added up, with docs-only
  pull requests apart from the ones that touch code;
* the queue wait (job created to job started, first attempts, not cancelled);
* the runs spent on a head that is a merge of main into the branch;
* the review rounds (comments that open with "## Revisão") and the commits pushed after the first;
* the intermittent failures (a job that failed and passed on a later attempt of the same head).

Standard library only; `gh` must be authenticated.
"""

from __future__ import annotations

import argparse
import json
import statistics
import subprocess
import sys
import time
from dataclasses import dataclass, field
from datetime import datetime
from typing import Any, cast

from steps import changes

REPOSITORY = "vitorparras/Conversational-Dataset-Generator"
WORKFLOW = "Verifica código"
OTHER_WORKFLOWS = ("CodeQL", "Verifica documentação")
KEPT_FOR_DOCUMENTS = (
    "Dependências vulneráveis nas duas árvores",
    "Segredos no histórico (gitleaks)",
    "Dependências, segredos e configuração (trivy)",
    "Análise estática de segurança (semgrep)",
    "Teto do pull request",
    "Commits sem atribuição a IA",
    "Classificar o diff (jobs pesados do código)",
)
OTHER_PREFIXES = tuple(f"{name}: " for name in OTHER_WORKFLOWS)
REVIEW_OPENING = "## Revisão"
Json = dict[str, Any]


def gh(*arguments: str) -> str:
    """The output of `gh`, tried three times because the API drops a call now and then."""
    error = ""
    for attempt in range(3):
        done = subprocess.run(
            ["gh", *arguments], check=False, capture_output=True, text=True, encoding="utf-8"
        )
        if done.returncode == 0:
            return done.stdout
        error = done.stderr.strip()
        time.sleep(2 * (attempt + 1))
    raise RuntimeError(f"gh {' '.join(arguments)} falhou: {error}")


def gh_pages(path: str) -> list[Json]:
    """Every page of a list endpoint, one object per page."""
    return cast("list[Json]", json.loads(gh("api", "--paginate", "--slurp", path)))


def gh_list(path: str) -> list[Json]:
    """Every item of an endpoint that answers with a plain list."""
    return [item for page in gh_pages(path) for item in cast("list[Json]", page)]


def moment(text: str) -> datetime:
    return datetime.fromisoformat(text.replace("Z", "+00:00"))


def seconds(start: str | None, end: str | None) -> float:
    return (moment(end) - moment(start)).total_seconds() if start and end else 0.0


@dataclass(frozen=True)
class JobStat:
    """One job of one attempt: the name, the seconds running, the seconds queued, the outcome."""

    name: str
    run_seconds: float
    queue_seconds: float
    conclusion: str
    attempt: int
    head: str


@dataclass
class PullRequest:
    number: int
    docs_only: bool
    files: int
    minutes_open: float
    jobs: list[JobStat] = field(default_factory=list[JobStat])
    heads: set[str] = field(default_factory=set[str])
    merge_heads: set[str] = field(default_factory=set[str])
    review_rounds: int = 0
    commits_after_review: int = 0
    flakes: list[str] = field(default_factory=list[str])

    @property
    def minutes(self) -> float:
        return sum(j.run_seconds for j in self.jobs) / 60

    @property
    def merge_minutes(self) -> float:
        return sum(j.run_seconds for j in self.jobs if j.head in self.merge_heads) / 60

    @property
    def dispensable_minutes(self) -> float:
        """Minutes of the jobs a documents-only diff may skip (not the kept ones nor CodeQL)."""
        return (
            sum(
                j.run_seconds
                for j in self.jobs
                if j.name not in KEPT_FOR_DOCUMENTS and not j.name.startswith(OTHER_PREFIXES)
            )
            / 60
        )

    @property
    def queue(self) -> list[float]:
        """First attempts only: a re-run keeps the first creation time, so it reads negative."""
        return [
            max(0.0, j.queue_seconds) / 60
            for j in self.jobs
            if j.attempt == 1 and j.conclusion not in ("skipped", "cancelled")
        ]


def find_flakes(jobs: list[JobStat]) -> list[str]:
    """Jobs that failed on one attempt and passed on a later attempt of the same head."""
    return [
        f"{job.name} @{job.head[:8]}"
        for job in jobs
        if job.conclusion == "failure"
        and any(
            o.name == job.name
            and o.head == job.head
            and o.attempt > job.attempt
            and o.conclusion == "success"
            for o in jobs
        )
    ]


def runs_of(branch: str, created: str, merged: str) -> list[Json]:
    """The runs of the checks, pull-request event, created while the pull request was open."""
    path = f"repos/{REPOSITORY}/actions/runs?branch={branch}&event=pull_request&per_page=100"
    runs = [r for page in gh_pages(path) for r in cast("list[Json]", page["workflow_runs"])]
    return [
        r
        for r in runs
        if r["name"] in (WORKFLOW, *OTHER_WORKFLOWS)
        and moment(created) <= moment(r["created_at"]) <= moment(merged)
    ]


def load(summary: Json) -> PullRequest:
    number = int(summary["number"])
    paths = [str(f["path"]) for f in summary["files"]]
    pr = PullRequest(
        number,
        bool(paths) and all(changes.is_documentation(p) for p in paths),
        len(paths),
        seconds(summary["createdAt"], summary["mergedAt"]) / 60,
    )
    commits = gh_list(f"repos/{REPOSITORY}/pulls/{number}/commits?per_page=100")
    merges = {str(c["sha"]) for c in commits if len(c["parents"]) > 1}
    comments = gh_list(f"repos/{REPOSITORY}/issues/{number}/comments?per_page=100")
    reviews = [str(c["created_at"]) for c in comments if str(c["body"]).startswith(REVIEW_OPENING)]
    pr.review_rounds = len(reviews)
    if reviews:
        pr.commits_after_review = sum(
            1
            for c in commits
            if len(c["parents"]) == 1 and str(c["commit"]["committer"]["date"]) > min(reviews)
        )
    for run in runs_of(str(summary["headRefName"]), summary["createdAt"], summary["mergedAt"]):
        head = str(run["head_sha"])
        pr.heads.add(head)
        if head in merges:
            pr.merge_heads.add(head)
        for attempt in range(1, int(run["run_attempt"]) + 1):
            path = f"repos/{REPOSITORY}/actions/runs/{run['id']}/attempts/{attempt}/jobs"
            for page in gh_pages(f"{path}?per_page=100"):
                for job in cast("list[Json]", page["jobs"]):
                    prefix = "" if run["name"] == WORKFLOW else f"{run['name']}: "
                    pr.jobs.append(
                        JobStat(
                            prefix + str(job["name"]),
                            seconds(job["started_at"], job["completed_at"]),
                            seconds(job["created_at"], job["started_at"]),
                            str(job["conclusion"]),
                            attempt,
                            head,
                        )
                    )
    pr.flakes = find_flakes(pr.jobs)
    return pr


def table(header: list[str], rows: list[list[str]]) -> str:
    lines = ["| " + " | ".join(header) + " |", "|" + "|".join("---" for _ in header) + "|"]
    return "\n".join([*lines, *("| " + " | ".join(row) + " |" for row in rows)])


def percentile(values: list[float], share: float) -> float:
    ordered = sorted(values)
    return ordered[min(len(ordered) - 1, int(share * len(ordered)))] if ordered else 0.0


def per_pull_request(prs: list[PullRequest]) -> str:
    return table(
        [
            "PR",
            "tipo",
            "arquivos",
            "min aberto",
            "heads",
            "heads de merge",
            "min de job",
            "fila média (min)",
            "rodadas de revisão",
            "commits após revisão",
            "intermitentes",
        ],
        [
            [
                f"#{p.number}",
                "docs" if p.docs_only else "código",
                str(p.files),
                f"{p.minutes_open:.0f}",
                str(len(p.heads)),
                str(len(p.merge_heads)),
                f"{p.minutes:.0f}",
                f"{statistics.mean(p.queue):.1f}" if p.queue else "-",
                str(p.review_rounds),
                str(p.commits_after_review),
                str(len(p.flakes)),
            ]
            for p in prs
        ],
    )


def per_job(prs: list[PullRequest]) -> str:
    rows: list[list[str]] = []
    for name in sorted({j.name for p in prs for j in p.jobs}):
        runs = [j for p in prs for j in p.jobs if j.name == name and j.conclusion != "skipped"]
        minutes = [j.run_seconds / 60 for j in runs]
        rows.append(
            [
                name,
                str(len(runs)),
                f"{statistics.median(minutes):.1f}" if minutes else "-",
                f"{percentile(minutes, 0.9):.1f}",
                f"{sum(minutes):.0f}",
                str(sum(1 for j in runs if j.conclusion == "cancelled")),
                str(sum(1 for j in runs if j.conclusion == "failure")),
            ]
        )
    rows.sort(key=lambda r: -float(r[4]))
    return table(
        ["job", "execuções", "mediana (min)", "p90 (min)", "total (min)", "canceladas", "falhas"],
        rows,
    )


def group_line(label: str, group: list[PullRequest]) -> str:
    if not group:
        return f"- {label}: nenhum PR"
    minutes = [p.minutes for p in group]
    queue = [q for p in group for q in p.queue]
    return (
        f"- {label}: {len(group)} PRs, {sum(minutes):.0f} min de job no total, mediana "
        f"{statistics.median(minutes):.0f} min por PR, fila média "
        f"{statistics.mean(queue):.1f} min (p90 {percentile(queue, 0.9):.1f})"
    )


def summary(prs: list[PullRequest]) -> str:
    total = sum(p.minutes for p in prs)
    merge = sum(p.merge_minutes for p in prs)
    flakes = [f"  - #{p.number}: {f}" for p in prs for f in p.flakes]
    docs = [p for p in prs if p.docs_only]
    return "\n".join(
        [
            group_line("PRs só de docs", [p for p in prs if p.docs_only]),
            group_line("PRs com código", [p for p in prs if not p.docs_only]),
            f"- heads que são merge do main: {sum(len(p.merge_heads) for p in prs)} de "
            f"{sum(len(p.heads) for p in prs)}; custaram {merge:.0f} de {total:.0f} min de job "
            f"({100 * merge / total if total else 0:.0f}%)",
            f"- PRs de documentos (.md de docs/ ou da raiz): {len(docs)}; minutos nos jobs "
            f"que o corte dispensa: {sum(p.dispensable_minutes for p in docs):.0f} de "
            f"{sum(p.minutes for p in docs):.0f}",
            f"- rodadas de revisão: {sum(p.review_rounds for p in prs)}; commits depois da "
            f"primeira revisão: {sum(p.commits_after_review for p in prs)}",
            f"- falhas intermitentes (falhou e passou na tentativa seguinte do mesmo head): "
            f"{len(flakes)}",
            *flakes,
        ]
    )


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--limit", type=int, default=20)
    arguments = parser.parse_args()
    listing = gh(
        "pr", "list", "--state", "merged", "-L", str(arguments.limit),
        "--json", "number,headRefName,createdAt,mergedAt,files",
    )  # fmt: skip
    prs = [load(s) for s in cast("list[Json]", json.loads(listing))]
    print(summary(prs), "", per_pull_request(prs), "", per_job(prs), sep="\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
