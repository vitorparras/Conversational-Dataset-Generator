"""The .NET projects a diff reaches: the touched ones and every project that depends on them.

Graph and diff reading are the verifier's (`steps.reach`, `steps.changes`). A path under no project
(`Directory.Build.props`, `global.json`, `.editorconfig`, the solution) reaches every project, as
does a diff that cannot be read: on doubt, the whole core.
"""

from __future__ import annotations

from collections.abc import Mapping
from dataclasses import dataclass
from pathlib import Path, PurePosixPath

from steps import changes, reach, test_core
from steps.common import Context

CORE = "core"
TESTS = (CORE, "tests")
SOURCES = (CORE, "src")
DATABASE = "Core.Tests.Database"
NOT_RUN = {"Core.Tests.Architecture": "roda o msbuild várias vezes: fica no job da CI"}
WHOLE = (*reach.WHOLE_NAMES, ".editorconfig")
WITH_DATABASE = "precisa do banco: fica no job rápido da CI"
PROJECT_DEPTH = 3


@dataclass(frozen=True)
class Scope:
    everything: bool
    reached: tuple[str, ...]
    leaves: tuple[str, ...]
    tests: tuple[str, ...]
    not_run: tuple[str, ...]
    files: Mapping[str, tuple[str, ...]]


def owner(path: str, graph: Mapping[str, object]) -> str | None:
    """The project of ``core/src`` or ``core/tests`` that owns a path, or None."""
    parts = PurePosixPath(path).parts
    if parts[:2] in (TESTS, SOURCES) and len(parts) > PROJECT_DEPTH and parts[2] in graph:
        return parts[2]
    return None


def reaches_all(path: str, graph: Mapping[str, object]) -> bool:
    """True for a path that touches the build of every project: shared settings, unplaced file."""
    pure = PurePosixPath(path)
    if pure.name.startswith(WHOLE) or pure.name in WHOLE:
        return True
    return pure.parts[0] == CORE and owner(path, graph) is None


def csharp_by_owner(
    root: Path, paths: list[str], graph: Mapping[str, object]
) -> dict[str, tuple[str, ...]]:
    """The C# files of the diff that still exist, by owning project."""
    found: dict[str, list[str]] = {}
    for path in paths:
        project = owner(path, graph)
        if project and path.endswith(".cs") and (root / path).is_file():
            found.setdefault(project, []).append(path)
    return {project: tuple(files) for project, files in sorted(found.items())}


def depending_on(graph: Mapping[str, set[str]], touched: set[str]) -> set[str]:
    return {name for name in graph if reach.closure(graph, [name]) & touched}


def leaves_of(graph: Mapping[str, set[str]], reached: set[str]) -> tuple[str, ...]:
    """The reached projects no other reached one references: building them builds the rest."""
    referenced = {ref for name in reached for ref in graph[name]}
    return tuple(sorted(reached - referenced))


def split_tests(
    root: Path, graph: Mapping[str, set[str]], reached: set[str]
) -> tuple[list[str], list[str]]:
    """The reached test projects to run, and the ones left out with the reason."""
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
    everything = not paths or any(reaches_all(path, graph) for path in paths)
    touched = {name for path in paths if (name := owner(path, graph))}
    reached = set(graph) if everything else depending_on(graph, touched)
    run, left = split_tests(ctx.root, graph, reached)
    files = csharp_by_owner(ctx.root, paths, graph)
    return Scope(
        everything,
        tuple(sorted(reached)),
        leaves_of(graph, reached),
        tuple(run),
        tuple(left),
        files,
    )
