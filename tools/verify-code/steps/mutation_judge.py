"""Judges the mutants of each critical module against the survivor ceiling of docs/operacao/014,
and the time each language took against its ceiling; prints every survivor by place and change."""

from __future__ import annotations

from collections.abc import Callable
from dataclasses import dataclass, replace
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


def in_turn[T](
    policy: MutationPolicy,
    targets: tuple[T, ...],
    area_of: Callable[[T], Area],
    mutate: Callable[[T, float], ModuleResult],
) -> tuple[tuple[ModuleResult, ...], tuple[str, ...], float]:
    """Every target in turn, each cut at what is left of the ceiling; then the rest stops. The
    results, the ceiling findings and the seconds spent."""
    results: list[ModuleResult] = []
    spent = 0.0
    for target in targets:
        left = policy.minutes * SECONDS_PER_MINUTE - spent
        measured = timed(lambda target=target, left=left: mutate(target, left))
        results.append(replace(measured.value, seconds=measured.seconds))
        spent += measured.seconds
        if over_ceiling(policy, spent):
            break
    areas = tuple(area_of(target) for target in targets)
    return tuple(results), ceiling_findings(policy, areas, results), spent


def ceiling_findings(
    policy: MutationPolicy, areas: tuple[Area, ...], results: list[ModuleResult]
) -> tuple[str, ...]:
    """The ceiling finding naming the module that passed it, and the modules left unrun."""
    spent = sum(result.seconds for result in results)
    if not over_ceiling(policy, spent):
        return ()
    left = tuple(f"`{area.prefix}`" for area in areas[len(results) :])
    passed = ceiling_problem(policy, (results[-1].area.prefix,), spent)
    return (passed, f"não rodaram por causa do teto: {', '.join(left)}") if left else (passed,)


def share(part: int, whole: int) -> str:
    return f"{WHOLE * part / whole if whole else 0.0:.1f} %".replace(".", ",")


def place(mutant: Mutant) -> str:
    return (
        f"{mutant.file}:{mutant.line}" if mutant.line else f"{mutant.file} (linha não localizada)"
    )


def outside(by: dict[MutantStatus, list[Mutant]]) -> tuple[str, ...]:
    """The mutants left out of the share, so a switch-off in the code stays visible."""
    broken, ignored = len(by[MutantStatus.NOT_COUNTED]), len(by[MutantStatus.IGNORED])
    static = by[MutantStatus.STATIC]
    notes: list[str] = []
    if broken or ignored:
        notes.append(f"  fora da conta: {broken} não compilam, {ignored} ignorados ou desligados")
    if static:
        places = ", ".join(place(m) for m in static)
        notes.append(f"  fora da conta: {len(static)} em inicializador estático ({places})")
    return tuple(notes)


def judge_module(result: ModuleResult, policy: MutationPolicy) -> Verdict:
    """Counts, survivors by place, and a failure above the ceiling, unchecked or empty."""
    by = {status: [m for m in result.mutants if m.status is status] for status in MutantStatus}
    killed, survived = by[MutantStatus.KILLED], by[MutantStatus.SURVIVED]
    generated = len(killed) + len(survived)
    name = f"{result.area.name} (`{result.area.prefix}`)"
    counts = f"{generated} gerados, {len(killed)} mortos, {len(survived)} sobreviventes"
    places = (f"  sobrevivente: {place(m)} — {m.change}" for m in survived)
    notes = (f"{name}: {counts} ({share(len(survived), generated)})", *places, *outside(by))
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
    each = ", ".join(f"`{r.area.prefix}` {minutes(r.seconds)}" for r in run.results)
    total = f"{run.side}: {minutes(run.seconds)} de {policy.minutes} min de teto"
    spent = (f"{run.side}, por módulo: {each}", total)
    own = (f"{run.side}: {problem}" for problem in run.problems)
    failures = (*own, *(failure for verdict in verdicts for failure in verdict.failures))
    notes = (*run.notes, *(n for v in verdicts for n in v.notes), *absent)
    return Verdict(failures, (*notes, *(spent if run.results else ())))
