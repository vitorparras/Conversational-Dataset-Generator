"""Runs mutmut over each critical service module in turn, in a copy of the service whose
configuration is generated from the list, with the test files that import the module and no others.
mutmut runs only on Linux; elsewhere the side is skipped with a warning."""

from __future__ import annotations

import ast
import shutil
import sys
import tomllib
from dataclasses import dataclass
from pathlib import Path, PurePosixPath

from steps.areas_doc import DOCUMENT, Area, area_of, within
from steps.build_service import REQUIREMENTS, SERVICE, UV
from steps.common import Context, reset_directory, tail
from steps.mutant import Mutant, MutantStatus
from steps.mutation_judge import LanguageRun, ModuleResult, in_turn
from steps.mutation_policy import MutationPolicy
from steps.mutmut_report import Entry, entries, status_of, survivor

SIDE = "serviço (Python)"
ROOT = "language_service"
RESULTS = "mutation/service"
SOURCES = "src"
TESTS = "tests"
PROJECT = "pyproject.toml"
CONFIG = "setup.cfg"
SKIPPED = shutil.ignore_patterns("__pycache__", ".venv")
#: These tests read the source text, which mutmut rewrites; they would fail on every mutant.
SOURCE_READING_TESTS = "tests/architecture"
TEST_FILES = ("test_*.py", "*_test.py")
LINUX = "linux"
PRAGMA = "pragma: no mutate"


def pattern(root: Path, area: Area) -> str | None:
    """The `only_mutate` glob of a module or package; None when the area has no code."""
    relative = PurePosixPath(SOURCES, *area.prefix.split("."))
    base = root / SERVICE / relative
    if base.with_suffix(".py").is_file():
        return f"{relative}.py"
    return f"{relative}/*" if base.is_dir() else None


@dataclass(frozen=True)
class ServiceTarget:
    """A critical module as mutmut sees it: its glob, the globs of the areas nested in it, which
    own their own code, and the test files that import it."""

    area: Area
    glob: str
    skip: tuple[str, ...]
    tests: tuple[str, ...]


def imported(source: str) -> frozenset[str]:
    """The dotted modules a file imports; `from a import b` also names `a.b`, a module or not."""
    try:
        tree = ast.parse(source)
    except (SyntaxError, ValueError):
        return frozenset()
    found: set[str] = set()
    for node in ast.walk(tree):
        if isinstance(node, ast.Import):
            found.update(alias.name for alias in node.names)
        elif isinstance(node, ast.ImportFrom) and node.module and not node.level:
            found.add(node.module)
            found.update(f"{node.module}.{alias.name}" for alias in node.names)
    return frozenset(found)


def test_files(root: Path, area: Area, everything: tuple[Area, ...]) -> tuple[str, ...]:
    """The test files, relative to the service, that import code the area owns by longest prefix.
    The tests that read the source text are left out."""
    service = root / SERVICE
    paths = sorted({p for glob in TEST_FILES for p in (service / TESTS).rglob(glob)})
    names = ((path, path.relative_to(service).as_posix()) for path in paths)
    kept = (
        name
        for path, name in names
        if not name.startswith(f"{SOURCE_READING_TESTS}/")
        and any(
            within(unit, ROOT) and area_of(unit, everything) == area
            for unit in imported(path.read_text(encoding="utf-8"))
        )
    )
    return tuple(kept)


def target(root: Path, area: Area, everything: tuple[Area, ...]) -> ServiceTarget | None:
    """The module's glob, what it leaves to the areas nested in it and its tests; None when the
    area has no code."""
    glob = pattern(root, area)
    if glob is None:
        return None
    inner = (a for a in everything if a != area and within(a.prefix, area.prefix))
    skip = tuple(g for a in inner if (g := pattern(root, a)) is not None)
    return ServiceTarget(area, glob, skip, test_files(root, area, everything))


def option(key: str, values: tuple[str, ...]) -> str:
    """One multi-line option of setup.cfg; nothing when there is no value."""
    return f"{key} =" + "".join(f"\n    {value}" for value in values) + "\n" if values else ""


def configuration(service: ServiceTarget) -> str:
    """The setup.cfg that tells mutmut what to mutate and which tests to run; the repository holds
    no copy of it."""
    header = f"[mutmut]\nsource_paths = {SOURCES}\n"
    only = option("only_mutate", (service.glob,))
    skip = option("do_not_mutate", service.skip)
    return header + only + skip + option("pytest_add_cli_args_test_selection", service.tests)


def workspace(ctx: Context, service: ServiceTarget) -> Path:
    """A fresh copy of the service with the generated configuration beside it."""
    folder = ctx.out_dir / RESULTS / service.area.prefix
    reset_directory(folder)
    source = ctx.root / SERVICE
    shutil.copytree(source / SOURCES, folder / SOURCES, ignore=SKIPPED)
    shutil.copytree(source / TESTS, folder / TESTS, ignore=SKIPPED)
    shutil.copy2(source / PROJECT, folder / PROJECT)
    (folder / CONFIG).write_text(configuration(service), encoding="utf-8")
    return folder


def mutmut(ctx: Context, *arguments: str) -> list[str]:
    return [*UV, "run", "--locked", "--project", str(ctx.root / SERVICE), "mutmut", *arguments]


def unit(entry: Entry) -> str:
    return ".".join(PurePosixPath(entry.file).with_suffix("").parts[1:])


def read(ctx: Context, folder: Path, entry: Entry) -> Mutant:
    """A killed or unchecked mutant as counted; a survivor placed by `mutmut show`."""
    status = status_of(entry.code)
    if status is not MutantStatus.SURVIVED:
        return Mutant(f"{SERVICE}/{entry.file}", 0, "", status)
    show = ctx.run(mutmut(ctx, "show", entry.name), folder)
    shown = show.text if show.ok else ""
    source = (ctx.root / SERVICE / entry.file).read_text(encoding="utf-8")
    return survivor(entry, shown, source, SERVICE)


def split(
    ctx: Context, folder: Path, area: Area, everything: tuple[Area, ...]
) -> tuple[Mutant, ...]:
    """The mutants the run left in the files the area owns by longest prefix."""
    owned = (entry for entry in entries(folder) if area_of(unit(entry), everything) == area)
    return tuple(read(ctx, folder, entry) for entry in owned)


def own_configuration(root: Path) -> tuple[str, ...]:
    """A problem per pyproject.toml of the service with a `[tool.mutmut]` section: mutmut reads it
    instead of the generated setup.cfg, so the scope would come from the tool's configuration."""
    service = root / SERVICE
    found = (
        service / PROJECT,
        *service.glob(f"{SOURCES}/**/{PROJECT}"),
        *service.glob(f"{TESTS}/**/{PROJECT}"),
    )
    return tuple(
        f"{path.relative_to(root).as_posix()} tem `[tool.mutmut]`; o escopo vem de {DOCUMENT}"
        for path in found
        if path.is_file()
        and "mutmut" in tomllib.loads(path.read_text(encoding="utf-8")).get("tool", {})
    )


def pragmas(root: Path, glob: str) -> int:
    """Lines of the module that mutmut leaves out because of a `pragma: no mutate` comment."""
    base = root / SERVICE / glob.removesuffix("/*")
    files = (base,) if glob.endswith(".py") else tuple(base.rglob("*.py"))
    return sum(path.read_text(encoding="utf-8").count(PRAGMA) for path in files)


def mutate(
    ctx: Context, service: ServiceTarget, everything: tuple[Area, ...], seconds: float
) -> ModuleResult:
    """Writes the module's workspace, runs mutmut for at most ``seconds``, reads what it left."""
    area = service.area
    if not service.tests:
        return ModuleResult(area, (), (f"nenhum teste de {TESTS}/ importa {area.prefix}",))
    folder = workspace(ctx, service)
    outcome = ctx.run_for(mutmut(ctx, "run", "--max-children", "1"), folder, seconds)
    mutants = split(ctx, folder, area, everything)
    install = f"uv ausente? instale com `pip install -r {REQUIREMENTS}`"
    problems = () if outcome.ok else ("o mutmut terminou com erro", install, *tail(outcome.text))
    return ModuleResult(area, mutants, problems)


def run_service(
    ctx: Context, areas: tuple[Area, ...], everything: tuple[Area, ...], policy: MutationPolicy
) -> LanguageRun:
    """Every service module with code in turn, each cut at what is left of the ceiling."""
    targets = tuple(s for area in areas if (s := target(ctx.root, area, everything)) is not None)
    absent = tuple(area for area in areas if area not in {s.area for s in targets})
    if not targets:
        return LanguageRun(SIDE, absent=absent)
    if sys.platform != LINUX:
        skip = f"AVISO — {SIDE}: o mutmut só roda no Linux; pulado em {sys.platform}"
        return LanguageRun(SIDE, absent=absent, notes=(skip,))
    if refused := own_configuration(ctx.root):
        return LanguageRun(SIDE, absent=absent, problems=refused)
    results, findings, spent = in_turn(
        policy, targets, lambda s: s.area, lambda s, seconds: mutate(ctx, s, everything, seconds)
    )
    off = (
        f"{s.area.name} (`{s.area.prefix}`): {n} `{PRAGMA}` fora da mutação"
        for s in targets
        if (n := pragmas(ctx.root, s.glob))
    )
    return LanguageRun(SIDE, results, absent, findings, tuple(off), spent)
