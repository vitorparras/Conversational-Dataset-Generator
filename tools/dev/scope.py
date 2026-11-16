"""The .NET projects a diff reaches: the touched ones and every project that depends on them.

A path under no project (`Directory.Build.props`, `global.json`, the solution) reaches every
project, as does a diff that cannot be read: on doubt, the whole core.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path, PurePosixPath

from steps import changes, reach, test_core
from steps.common import Context

CORE = "core"
DATABASE = "Core.Tests.Database"
NOT_RUN = {"Core.Tests.Architecture": "roda o msbuild várias vezes: fica no job da CI"}
WHOLE = (*reach.WHOLE_NAMES, ".editorconfig")
WITH_DATABASE = "precisa do banco: fica no job rápido da CI"


@dataclass(frozen=True)
class Scope:
    everything: bool
    reached: tuple[str, ...]
    leaves: tuple[str, ...]
    tests: tuple[str, ...]
    not_run: tuple[str, ...]
    files: tuple[str, ...]


def reaches_all(path: str, graph: reach.Graph) -> bool:
    pure = PurePosixPath(path)
    unplaced = pure.parts[0] == CORE and reach.project_of(path, graph) is None
    return pure.name.startswith(WHOLE) or pure.name in WHOLE or unplaced


def csharp_files(root: Path, paths: list[str], graph: reach.Graph) -> tuple[str, ...]:
    return tuple(
        path
        for path in paths
        if path.endswith(".cs") and reach.project_of(path, graph) and (root / path).is_file()
    )


def depending_on(graph: reach.Graph, touched: set[str]) -> set[str]:
    return {name for name in graph if reach.closure(graph, [name]) & touched}


def leaves_of(graph: reach.Graph, reached: set[str]) -> tuple[str, ...]:
    referenced = {ref for name in reached for ref in graph[name]}
    return tuple(sorted(reached - referenced))


def split_tests(root: Path, graph: reach.Graph, reached: set[str]) -> tuple[list[str], list[str]]:
    run: list[str] = []
    left: list[str] = []
    for name in sorted(reached):
        project = root / CORE / "tests" / name / f"{name}.csproj"
        if not project.is_file() or not test_core.is_test_project(project):
            continue
        why = NOT_RUN.get(name) or (
            WITH_DATABASE if DATABASE in reach.closure(graph, [name]) else ""
        )
        if why:
            left.append(f"{name}: {why}")
        else:
            run.append(name)
    return run, left


def compute(ctx: Context) -> Scope:
    graph = reach.projects(ctx.root)
    found = changes.changed_paths(ctx)
    paths = [] if isinstance(found, str) else list(found)
    everything = isinstance(found, str) or any(reaches_all(path, graph) for path in paths)
    touched = {name for path in paths if (name := reach.project_of(path, graph))}
    reached = set(graph) if everything else depending_on(graph, touched)
    run, left = split_tests(ctx.root, graph, reached)
    files = csharp_files(ctx.root, paths, graph)
    leaves = leaves_of(graph, reached)
    return Scope(everything, tuple(sorted(reached)), leaves, tuple(run), tuple(left), files)
