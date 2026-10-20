"""Runs mutmut over the critical service modules, in a copy of the service whose configuration is
generated from the list. mutmut runs only on Linux; elsewhere the side is skipped with a warning."""

from __future__ import annotations

import shutil
import sys
from pathlib import Path, PurePosixPath

from steps.areas_doc import Area, area_of
from steps.build_service import REQUIREMENTS, SERVICE, UV
from steps.common import Context, reset_directory, tail
from steps.mutant import Mutant, MutantStatus
from steps.mutation_judge import LanguageRun, ModuleResult, ceiling_problem, over_ceiling, timed
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
LINUX = "linux"


def pattern(root: Path, area: Area) -> str | None:
    """The `only_mutate` glob of a module or package; None when the area has no code."""
    relative = PurePosixPath(SOURCES, *area.prefix.split("."))
    base = root / SERVICE / relative
    if base.with_suffix(".py").is_file():
        return f"{relative}.py"
    return f"{relative}/*" if base.is_dir() else None


def configuration(patterns: tuple[str, ...]) -> str:
    """The setup.cfg that tells mutmut what to mutate; the repository holds no copy of it."""
    only = "".join(f"\n    {glob}" for glob in patterns)
    ignore = f"pytest_add_cli_args =\n    --ignore={SOURCE_READING_TESTS}"
    return f"[mutmut]\nsource_paths = {SOURCES}\nonly_mutate ={only}\n{ignore}\n"


def workspace(ctx: Context, patterns: tuple[str, ...]) -> Path:
    """A fresh copy of the service with the generated configuration beside it."""
    folder = ctx.out_dir / RESULTS
    reset_directory(folder)
    service = ctx.root / SERVICE
    shutil.copytree(service / SOURCES, folder / SOURCES, ignore=SKIPPED)
    shutil.copytree(service / TESTS, folder / TESTS, ignore=SKIPPED)
    shutil.copy2(service / PROJECT, folder / PROJECT)
    (folder / CONFIG).write_text(configuration(patterns), encoding="utf-8")
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
    shown = ctx.run(mutmut(ctx, "show", entry.name), folder).text
    source = (ctx.root / SERVICE / entry.file).read_text(encoding="utf-8")
    return survivor(entry, shown, source, SERVICE)


def split(ctx: Context, folder: Path, modules: tuple[Area, ...]) -> tuple[ModuleResult, ...]:
    """The mutants the run left, each under the module that owns its file."""
    owned = [(area_of(unit(entry), modules), entry) for entry in entries(folder)]
    mutants = [(owner, read(ctx, folder, entry)) for owner, entry in owned]
    return tuple(ModuleResult(area, tuple(m for o, m in mutants if o == area)) for area in modules)


def run_service(ctx: Context, areas: tuple[Area, ...], policy: MutationPolicy) -> LanguageRun:
    """One mutmut run over every module with code, its mutants split back by module."""
    found = tuple((area, glob) for area in areas if (glob := pattern(ctx.root, area)) is not None)
    modules = tuple(area for area, _ in found)
    absent = tuple(area for area in areas if area not in modules)
    if not found:
        return LanguageRun(SIDE, absent=absent)
    if sys.platform != LINUX:
        skip = f"AVISO — {SIDE}: o mutmut só roda no Linux; pulado em {sys.platform}"
        return LanguageRun(SIDE, absent=absent, notes=(skip,))
    folder = workspace(ctx, tuple(glob for _, glob in found))
    measured = timed(lambda: ctx.run(mutmut(ctx, "run", "--max-children", "1"), folder))
    results = split(ctx, folder, modules)
    problems: list[str] = []
    if not measured.value.ok:
        install = f"uv ausente? instale com `pip install -r {REQUIREMENTS}`"
        problems.extend(("o mutmut terminou com erro", install, *tail(measured.value.text)))
    if over_ceiling(policy, measured.seconds):
        prefixes = tuple(area.prefix for area in modules)
        problems.append(ceiling_problem(policy, prefixes, measured.seconds))
    return LanguageRun(SIDE, results, absent, tuple(problems), seconds=measured.seconds)
