"""Tests for the project graph against the real tree: no project, reference or area may fall out of
it, so that no critical module is left without mutation in silence."""

from __future__ import annotations

import unittest
from collections import defaultdict

from steps import mutation, mutation_core, mutation_service, reach
from steps.areas_doc import Area, read_areas, within

from . import support

ROOT = support.VERIFY_CODE.parents[1]
BUILT = {"bin", "obj"}
REFERENCE_TAG = "<ProjectReference"
FIXTURES = "fixtures"  # their paths are outside the graph, so a change there runs the whole ruler


class ProjectGraphTests(unittest.TestCase):
    def test_every_project_of_the_core_belongs_to_an_area(self) -> None:
        areas = read_areas(ROOT).areas
        sources = {p.parent.name for p in (ROOT / mutation_core.SOURCES).glob("*/*.csproj")}

        orphans = [n for n in sources if not any(within(a.prefix, n) for a in areas)]

        self.assertEqual(orphans, [], "projeto de core/src sem área em docs/operacao/areas")

    def test_every_area_of_the_core_or_the_service_points_to_code_that_exists(self) -> None:
        areas = read_areas(ROOT).areas
        projects = reach.projects(ROOT)
        units = reach.units(ROOT)

        missing = [
            a.prefix
            for a in areas
            if (
                within(a.prefix, mutation_core.ROOT)
                and not any(within(a.prefix, p) for p in projects)
            )
            or (
                within(a.prefix, mutation_service.ROOT)
                and not any(within(u, a.prefix) for u in units)
            )
        ]

        self.assertEqual(missing, [], "área que aponta para projeto ou pacote que não existe")

    def test_an_area_outside_both_graphs_is_never_critical(self) -> None:
        roots = (mutation_core.ROOT, mutation_service.ROOT)
        areas = read_areas(ROOT).areas

        stray = [
            a.prefix for a in areas if a.critical and not any(within(a.prefix, r) for r in roots)
        ]

        self.assertEqual(stray, [])

    def test_no_project_file_sits_where_the_reader_does_not_look(self) -> None:
        found = {
            p.stem
            for p in (ROOT / "core").rglob("*.csproj")
            if BUILT.isdisjoint(p.relative_to(ROOT).parts) and FIXTURES not in p.parts
        }

        self.assertEqual(found, set(reach.projects(ROOT)))

    def test_every_project_reference_is_seen_by_the_reader_and_points_to_a_project(self) -> None:
        graph = reach.projects(ROOT)
        files = [
            p
            for p in (ROOT / "core").rglob("*.csproj")
            if BUILT.isdisjoint(p.parts) and FIXTURES not in p.parts
        ]
        written = sum(p.read_text(encoding="utf-8").count(REFERENCE_TAG) for p in files)
        seen = sum(len(found) for found in graph.values())
        unknown = {target for found in graph.values() for target in found} - set(graph)

        self.assertGreater(written, 0)
        self.assertEqual(seen, written, "ProjectReference que o leitor do grafo não vê")
        self.assertEqual(unknown, set(), "referência a projeto fora do grafo")

    def test_no_code_of_the_service_imports_in_a_way_the_reader_cannot_resolve(self) -> None:
        opaque = reach.opaque(reach.units(ROOT))

        self.assertEqual(opaque, [], "import acima do pacote, importlib ou __import__")

    def test_every_import_of_the_service_names_a_module_the_reader_knows(self) -> None:
        known = reach.units(ROOT)
        unknown = [
            f"{name} -> {target}"
            for name, (targets, _) in reach.scanned(known).items()
            for target in targets
            if within(target, mutation_service.ROOT)
            and not any(within(target, unit) or within(unit, target) for unit in known)
        ]

        self.assertEqual(unknown, [], "import do serviço para módulo que o leitor não conhece")

    def test_the_reference_of_every_test_project_to_a_core_project_is_in_the_graph(self) -> None:
        graph = reach.projects(ROOT)
        every = {p.stem for p in (ROOT / mutation_core.SOURCES).glob("*/*.csproj")}

        self.assertTrue(every <= set(graph))

    def test_every_critical_module_is_reached_by_its_own_code_and_its_own_tests(self) -> None:
        table = read_areas(ROOT)
        modules, _ = mutation.listed(table)
        context = support.context(ROOT)
        silent: list[str] = []
        for area in modules:
            for kind, name in self.own(area, table.areas):
                touched = defaultdict(set, {kind: {name}})
                if reach.reason(context, area, table.areas, touched) is None:
                    silent.append(f"{area.prefix}: {kind} {name}")

        self.assertEqual(silent, [], "módulo que a própria mudança não alcança")

    def own(self, area: Area, areas: tuple[Area, ...]) -> list[tuple[str, str]]:
        """A (kind, name) for the code of a module and for each of its tests."""
        if within(area.prefix, mutation_core.ROOT):
            core = mutation_core.whole(ROOT, area, areas)
            assert core is not None, f"{area.prefix} sem projeto"
            tests = mutation_core.test_projects(ROOT, core.project, area.prefix)
            self.assertTrue(tests, f"{area.prefix} sem projeto de teste")
            return [("project", core.project), *(("project", p.stem) for p in tests)]
        tests = mutation_service.test_files(ROOT, area, areas)
        self.assertTrue(tests, f"{area.prefix} sem teste")
        owned = next(u for u in reach.units(ROOT) if within(u, area.prefix))
        return [("unit", owned), *(("test", t) for t in tests)]


if __name__ == "__main__":
    unittest.main()
