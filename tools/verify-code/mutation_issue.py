"""Usage: mutation_issue.py <folder of the reports> <url of the run>. Each failing module of the
nightly mutation of main (decisão 0059) gets one issue, or a comment on its open one; a report is
the job in `modulo.txt` and what the step printed in `relatorio.txt`."""

from __future__ import annotations

import json
import sys
from collections import defaultdict
from pathlib import Path

from steps.common import RunCommand, run_process
from steps.mutation_parts import split_module

TITLE = "Mutação noturna reprovou em `{}`"
SURVIVOR = "sobrevivente:"
UNREPORTED = "sem relatório"
SHOWN_LINES = 60


def reports(folder: Path) -> dict[str, list[tuple[str, list[str]]]]:
    """The failing jobs by module: the name of each job and the lines its step printed."""
    found: dict[str, list[tuple[str, list[str]]]] = defaultdict(list)
    for module_file in sorted(folder.glob("*/modulo.txt")):
        job = module_file.read_text(encoding="utf-8").strip()
        report = module_file.with_name("relatorio.txt")
        lines = report.read_text(encoding="utf-8").splitlines() if report.is_file() else []
        found[split_module(job)[0] or job].append((job, lines))
    return found


def body(run: str, prefix: str, jobs: list[tuple[str, list[str]]]) -> str:
    """The text of the issue or of the comment: the module, the run, the survivors and the output."""
    survivors = [line.strip() for _, lines in jobs for line in lines if SURVIVOR in line]
    listed = [f"- {line}" for line in survivors] or [
        "- nenhum sobrevivente listado; a reprovação veio de outra causa, veja a saída abaixo"
    ]
    output = (f"{job}:\n" + "\n".join(lines[:SHOWN_LINES]) for job, lines in jobs)
    head = [f"Módulo: `{prefix}`", f"Run: {run}", f"Jobs: {', '.join(job for job, _ in jobs)}"]
    return "\n".join((*head, "", "Mutantes sobreviventes:", *listed, "", "```", *output, "```", ""))


def open_issue(title: str, folder: Path, run: RunCommand) -> int | str | None:
    """The number of the open issue with exactly ``title``, None when there is none, and the
    reason when GitHub cannot say."""
    asked = ["gh", "issue", "list", "--state", "open", "--json", "number,title"]
    outcome = run([*asked, "--limit", "1000"], folder)
    if not outcome.ok:
        return f"o gh não listou as issues abertas: {outcome.text.strip()}"
    issues = json.loads(outcome.text or "[]")
    return next((int(issue["number"]) for issue in issues if issue["title"] == title), None)


def file(prefix: str, text: str, folder: Path, run: RunCommand) -> str | None:
    """Comments on the open issue of the module or opens one; the reason when neither happened."""
    title = TITLE.format(prefix)
    found = open_issue(title, folder, run)
    if isinstance(found, str):
        return found
    written = folder / f"corpo-{prefix}.md"
    written.write_text(text, encoding="utf-8")
    command = ["gh", "issue", "create", "--title", title]
    if found is not None:
        command = ["gh", "issue", "comment", str(found)]
    outcome = run([*command, "--body-file", str(written)], folder)
    return None if outcome.ok else f"`{prefix}`: o gh recusou: {outcome.text.strip()}"


def main(argv: list[str], run: RunCommand = run_process) -> int:
    """Files every failing module, or the run when no job left a report; 1 when any of them
    could not be filed or no report came."""
    if len(argv) != 2:
        print(__doc__)
        return 2
    folder, url = Path(argv[0]), argv[1]
    folder.mkdir(parents=True, exist_ok=True)
    found = reports(folder) or {UNREPORTED: [("nenhum job deixou relatório; veja o run", [])]}
    problems = [
        problem
        for prefix, jobs in found.items()
        if (problem := file(prefix, body(url, prefix, jobs), folder, run))
    ]
    print("\n".join(problems or ["mutação noturna: issues registradas"]))
    return 1 if problems or UNREPORTED in found else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
