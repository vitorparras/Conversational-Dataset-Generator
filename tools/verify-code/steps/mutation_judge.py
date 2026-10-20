"""Judges the mutants of each critical module against the survivor ceiling of docs/operacao/014,
and the time each language took against its ceiling; prints every survivor by place and change."""

from __future__ import annotations

from collections.abc import Callable
from dataclasses import dataclass
from time import monotonic

from steps.areas_doc import Area
from steps.floors import Verdict
from steps.mutant import Mutant, MutantStatus
from steps.mutation_policy import MutationPolicy

SECONDS_PER_MINUTE = 60
WHOLE = 100


@dataclass(frozen=True)
class ModuleResult:
    """The mutants of one module, what kept them from being read and how long the run took."""

    area: Area
    mutants: tuple[Mutant, ...]
    problems: tuple[str, ...] = ()
    seconds: float = 0.0


@dataclass(frozen=True)
class LanguageRun:
    """One language's modules: those run, those with no code, and what failed or informs."""

    side: str
    results: tuple[ModuleResult, ...] = ()
    absent: tuple[Area, ...] = ()
    problems: tuple[str, ...] = ()
    notes: tuple[str, ...] = ()
    seconds: float = 0.0


@dataclass(frozen=True)
class Timed[T]:
    """What some work returned and the seconds it took."""

    value: T
    seconds: float


def timed[T](work: Callable[[], T]) -> Timed[T]:
    started = monotonic()
    value = work()
    return Timed(value, monotonic() - started)


def minutes(seconds: float) -> str:
    return f"{seconds / SECONDS_PER_MINUTE:.1f} min".replace(".", ",")


def over_ceiling(policy: MutationPolicy, seconds: float) -> bool:
    return seconds > policy.minutes * SECONDS_PER_MINUTE


def ceiling_problem(policy: MutationPolicy, prefixes: tuple[str, ...], seconds: float) -> str:
    """The finding of a ceiling passed: the modules running when it happened and the time spent."""
    named = ", ".join(f"`{prefix}`" for prefix in prefixes)
    return f"teto de {policy.minutes} min excedido em {named}: {minutes(seconds)} gastos"


def share(part: int, whole: int) -> str:
    return f"{WHOLE * part / whole if whole else 0.0:.1f} %".replace(".", ",")


def judge_module(result: ModuleResult, policy: MutationPolicy) -> Verdict:
    """Counts, survivors by place, and a failure above the ceiling, unchecked or empty."""
    by = {status: [m for m in result.mutants if m.status is status] for status in MutantStatus}
    killed, survived = by[MutantStatus.KILLED], by[MutantStatus.SURVIVED]
    generated = len(killed) + len(survived)
    name = f"{result.area.name} (`{result.area.prefix}`)"
    counts = f"{generated} gerados, {len(killed)} mortos, {len(survived)} sobreviventes"
    places = (f"  sobrevivente: {m.file}:{m.line} — {m.change}" for m in survived)
    notes = (f"{name}: {counts} ({share(len(survived), generated)})", *places)
    failures = [f"{name}: {problem}" for problem in result.problems]
    if by[MutantStatus.UNCHECKED]:
        failures.append(f"{name}: {len(by[MutantStatus.UNCHECKED])} mutantes não verificados")
    if generated == 0:
        failures.append(f"{name}: nenhum mutante gerado; o passo não passa vazio")
    if len(survived) * WHOLE > policy.survivor_ceiling * generated:
        ceiling = f"passa do limite de {policy.survivor_ceiling} %"
        failures.append(f"{name}: {share(len(survived), generated)} de sobreviventes {ceiling}")
    return Verdict(tuple(failures), notes)


def judge_language(run: LanguageRun, policy: MutationPolicy) -> Verdict:
    """The verdicts of a language's modules, with its own problems, notes and time."""
    verdicts = [judge_module(result, policy) for result in run.results]
    absent = (
        f"{a.name} (`{a.prefix}`): sem código; a mutação vale quando houver" for a in run.absent
    )
    spent = (f"{run.side}: {minutes(run.seconds)} de {policy.minutes} min de teto",)
    own = (f"{run.side}: {problem}" for problem in run.problems)
    failures = (*own, *(failure for verdict in verdicts for failure in verdict.failures))
    notes = (*run.notes, *(n for v in verdicts for n in v.notes), *absent)
    return Verdict(failures, (*notes, *(spent if run.results else ())))
