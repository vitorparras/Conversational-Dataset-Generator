"""Picks the critical modules a pull request reaches, so CI mutates only those and says why.

A module is reached when the diff has: (a) a file of its project; (b) a file of a project it
references, directly or not; (c) a test project that exercises it (or one that project references);
(d) its own area file in docs/operacao/areas; (e) in the language service, a package it imports or a
test that imports it. Anything the graph cannot classify (``Directory.Build.*``, ``global.json``,
``packages.lock.json``, ``uv.lock``, the workflow, ``tools/verify-code``, ``core/Core.slnx``...), an
empty or unreadable diff and every doubt reach every module, as does an ``importlib`` or
``__import__`` the reader cannot follow. The graph is read from the files.
"""

from __future__ import annotations

import ast
import re
from collections import defaultdict
from collections.abc import Iterable, Mapping
from fnmatch import fnmatch
from pathlib import Path, PurePosixPath, PureWindowsPath

from steps import changes, mutation, mutation_core, mutation_service
from steps.areas_doc import FOLDER, Area, read_areas, within
from steps.build_service import SERVICE
from steps.common import Context

REFERENCE = re.compile(r'<ProjectReference\s+Include="([^"]+)"')
DYNAMIC = "importlib"
BUILTIN = "__import__"
WHOLE_NAMES = ("packages.lock.json", "global.json", "uv.lock", "Core.slnx", "Directory.")
SERVICE_SOURCES = f"{SERVICE}/{mutation_service.SOURCES}"
CORE_TREES = (("core", "src"), ("core", "tests"))
PROJECT_FILE = 3
Graph = Mapping[str, Iterable[str]]
Touched = defaultdict[str, set[str]]


def projects(root: Path) -> dict[str, set[str]]:
    """Each project of core/src and core/tests with the names of the projects it references."""
    found: dict[str, set[str]] = {}
    for tree in (mutation_core.SOURCES, mutation_core.TESTS):
        for csproj in sorted((root / tree).glob("*/*.csproj")):
            text = csproj.read_text(encoding="utf-8")
            found[csproj.stem] = {PureWindowsPath(p).stem for p in REFERENCE.findall(text)}
    return found


def units(root: Path) -> dict[str, Path]:
    """The dotted name of each Python module of the service, with its file."""
    base = root / SERVICE_SOURCES
    parts = {p: p.relative_to(base).with_suffix("").parts for p in sorted(base.rglob("*.py"))}
    return {".".join(n[:-1] if n[-1] == "__init__" else n): p for p, n in parts.items()}


def targets(parent: list[str], source: str) -> tuple[set[str], bool]:
    """The dotted names a module imports, relative imports resolved from the package ``parent``,
    and whether it also imports in a way the reader cannot follow (``importlib``, ``__import__``,
    a relative import above the top package, a source that does not parse)."""
    try:
        tree = ast.parse(source)
    except (SyntaxError, ValueError):
        return set(), True
    found: set[str] = set()
    opaque = False
    for node in ast.walk(tree):
        if isinstance(node, ast.Import):
            found.update(alias.name for alias in node.names)
            opaque |= any(alias.name.split(".")[0] == DYNAMIC for alias in node.names)
        elif isinstance(node, ast.ImportFrom):
            base = parent[: len(parent) - node.level + 1] if node.level else []
            module = [*base, *(node.module.split(".") if node.module else [])]
            opaque |= node.level > len(parent) or (node.module or "").split(".")[0] == DYNAMIC
            found.add(".".join(module))
            found.update(f"{'.'.join(module)}.{alias.name}" for alias in node.names)
        elif isinstance(node, ast.Name):
            opaque |= node.id == BUILTIN
    return found, opaque


def scanned(known: Mapping[str, Path]) -> dict[str, tuple[set[str], bool]]:
    """What each module of the service imports and whether the reader can follow all of it."""
    found: dict[str, tuple[set[str], bool]] = {}
    for name, path in known.items():
        parent = name.split(".") if path.name == "__init__.py" else name.split(".")[:-1]
        found[name] = targets(parent, path.read_text(encoding="utf-8", errors="replace"))
    return found


def opaque(known: Mapping[str, Path]) -> list[str]:
    """The modules of the service whose imports the reader cannot resolve."""
    return sorted(name for name, (_, doubt) in scanned(known).items() if doubt)


def imports(known: Mapping[str, Path]) -> dict[str, set[str]]:
    """The modules of the service each one imports, with the packages those imports run through."""
    edges: dict[str, set[str]] = {}
    for name, (names, _) in scanned(known).items():
        edges[name] = {
            ".".join(parts[:n])
            for parts in (imported.split(".") for imported in names)
            for n in range(1, len(parts) + 1)
            if ".".join(parts[:n]) in known
        } - {name}
    return edges


def closure(graph: Graph, start: Iterable[str]) -> set[str]:
    """The nodes ``start`` reaches through ``graph``, itself included."""
    seen: set[str] = set()
    todo = list(start)
    while todo:
        node = todo.pop()
        if node not in seen:
            seen.add(node)
            todo.extend(graph.get(node, ()))
    return seen


def in_service(path: str, known: Mapping[str, Path]) -> tuple[str, str] | None:
    """The module or test of the language service a changed path is, or None."""
    pure = PurePosixPath(path)
    unit = ".".join(pure.with_suffix("").parts[2:]).removesuffix(".__init__")
    if path.startswith(f"{SERVICE_SOURCES}/") and pure.suffix == ".py" and unit in known:
        return "unit", unit
    tests = f"{SERVICE}/{mutation_service.TESTS}/"
    if path.startswith(tests) and any(fnmatch(pure.name, g) for g in mutation_service.TEST_FILES):
        return "test", path.removeprefix(f"{SERVICE}/")
    return None


def classify(
    ctx: Context, path: str, known: Mapping[str, Path], graph: Graph
) -> tuple[str, str] | None:
    """The (kind, name) the graph knows a changed path by, or None when it cannot place it."""
    pure = PurePosixPath(path)
    parts = pure.parts
    if pure.name.startswith(WHOLE_NAMES) or pure.name in WHOLE_NAMES:
        return None
    if path.startswith(f"{FOLDER}/") and pure.suffix == ".md":
        return "area", pure.stem
    if changes.is_documentation(path):
        return None if changes.read_by_code(ctx, path) else ("doc", path)
    if parts[:2] in CORE_TREES and len(parts) > PROJECT_FILE and parts[2] in graph:
        return "project", parts[2]
    return in_service(path, known)


def why(graph: Graph, code: set[str], tests: set[str], touched: set[str]) -> str | None:
    """Which rule (a) to (c) or (e) reaches the module, or None when none does."""
    if hit := code & touched:
        return f"arquivo de {', '.join(sorted(hit))}"
    if hit := closure(graph, code) & touched:
        return f"depende de {', '.join(sorted(hit))}, que mudou"
    if hit := closure(graph, tests) & touched:
        return f"o teste {', '.join(sorted(hit))} mudou"
    return None


def reason(ctx: Context, area: Area, everything: tuple[Area, ...], touched: Touched) -> str | None:
    """Why the pull request reaches the module (a line for the reviewer), or None if it does not."""
    changed = sorted(name for name in touched["area"] if within(name, area.prefix))
    if changed:
        return f"a área {changed[0]} em {FOLDER} mudou"
    if within(area.prefix, mutation_core.ROOT):
        core = mutation_core.whole(ctx.root, area, everything)
        if core is None:
            return "sem projeto no grafo: na dúvida, roda"
        found = mutation_core.test_projects(ctx.root, core.project, area.prefix)
        return why(projects(ctx.root), {core.project}, {p.stem for p in found}, touched["project"])
    if within(area.prefix, mutation_service.ROOT):
        known = units(ctx.root)
        owned = {name for name in known if within(name, area.prefix)}
        tests = set(mutation_service.test_files(ctx.root, area, everything))
        return why(imports(known), owned, tests, touched["unit"] | touched["test"])
    return "fora dos mapas de projeto e de pacote: na dúvida, roda"


def select(ctx: Context) -> tuple[tuple[str, ...], list[str]]:
    """The mutation jobs the pull request reaches and the lines that say why; every job on doubt."""
    table = read_areas(ctx.root)
    modules, _ = mutation.listed(table)
    names = {a.prefix: mutation.jobs(ctx.root, a, table.areas) for a in modules}
    everything = tuple(n for found in names.values() for n in found)
    paths = changes.changed_paths(ctx)
    if isinstance(paths, str) or not paths:
        return everything, [f"régua inteira: {paths or 'o diff está vazio'}"]
    graph, known = projects(ctx.root), units(ctx.root)
    if doubtful := opaque(known):
        return everything, [f"régua inteira: o leitor não resolve os imports de {doubtful[0]}"]
    touched: Touched = defaultdict(set)
    for path in paths:
        placed = classify(ctx, path, known, graph)
        if placed is None:
            return everything, [f"régua inteira: `{path}` está fora do grafo ou dispara a régua"]
        touched[placed[0]].add(placed[1])
    lines = {a.prefix: reason(ctx, a, table.areas, touched) for a in modules}
    chosen = [a.prefix for a in modules if lines[a.prefix]]
    shown = [f"- `{prefix}`: {lines[prefix]}" for prefix in chosen]
    return tuple(n for prefix in chosen for n in names[prefix]), shown or [
        "nenhum módulo alcançado"
    ]
