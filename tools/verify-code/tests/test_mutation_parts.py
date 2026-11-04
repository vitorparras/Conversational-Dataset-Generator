"""Tests for the parts of a critical area's mutation (#580): the line of the area file, the split of
its files, the names of the matrix and the `--module` that names one part."""

from __future__ import annotations

import itertools
import json
import re
import unittest
from pathlib import Path

from steps import areas_doc, mutation, mutation_core, mutation_parts
from steps.mutation_members import UNBOUNDED
from steps.mutation_parts import Part, Slice

from . import mutation_fixture as fixture
from . import support

LOGGING = "Core.Platform.Logging"
INNER = f"{LOGGING}.Inner"
FILES = ("Alpha", "Beta", "Delta", "Formatter", "Gamma")
HIDDEN = "Logging/Inner/Hidden.cs"
FOLDER = areas_doc.FOLDER


def area_file(prefix: str, critical: str = "sim", parts: str | None = None) -> str:
    """The text of one area file, with the line of the parts when ``parts`` is given."""
    own = [] if parts is None else [f"- Partes da mutação: {parts}"]
    head = [f"# {prefix}", "", "> Operacional.", "", f"- Projeto/pacote: `{prefix}`"]
    lines = [*head, "- Piso: 90 %"]
    return "\n".join((*lines, f"- Crítica: {critical}", *own, "- Motivo: teste", ""))


class PartsFixture(fixture.MutationFixture):
    """The synthetic repository with five files in the logging area and one in an area inside it."""

    def setUp(self) -> None:
        super().setUp()
        for name in FILES:
            support.write(self.root, f"core/src/Core.Platform/Logging/{name}.cs", "class C {}\n")
        support.write(self.root, f"core/src/Core.Platform/{HIDDEN}", "class H {}\n")
        support.write(self.root, f"{FOLDER}/{INNER}.md", area_file(INNER, "não"))

    def declare(self, parts: str | None, prefix: str = LOGGING) -> None:
        """Gives the area the line of the parts, or none."""
        support.write(self.root, f"{FOLDER}/{prefix}.md", area_file(prefix, "sim", parts))

    def area(self) -> areas_doc.Area:
        return next(a for a in areas_doc.read_areas(self.root).areas if a.prefix == LOGGING)

    def patterns(self, number: int, of: int) -> tuple[str, ...]:
        """The files of the part of the logging area, as the target of the step holds them."""
        every = areas_doc.read_areas(self.root).areas
        core = mutation_core.target(self.root, self.area(), every, Part(number, of))
        return () if core is None else core.patterns

    def mutated(self, runner: fixture.ToolRunner) -> list[list[str]]:
        """The files each Stryker call was configured to mutate, in the order of the calls."""
        configs = (Path(call[call.index("--config-file") + 1]) for call in runner.called("stryker"))
        texts = (c.read_text(encoding="utf-8") for c in configs)
        return [json.loads(text)["stryker-config"]["mutate"] for text in texts]


class PartsOfTheAreaTests(PartsFixture):
    def test_an_area_without_the_line_is_one_part_with_the_name_it_always_had(self) -> None:
        self.declare(None)

        names, problems = mutation.names(self.root)

        self.assertEqual((problems, LOGGING in names), ((), True))
        self.assertFalse(any(mutation_parts.SEPARATOR in name for name in names))

    def test_the_line_gives_one_name_per_part_in_the_place_of_the_area(self) -> None:
        self.declare("3")

        names, _ = mutation.names(self.root)

        expected = (f"{LOGGING}@1/3", f"{LOGGING}@2/3", f"{LOGGING}@3/3")
        self.assertEqual(names[1:4], expected)
        self.assertEqual((names[0], names[4]), ("Core.Boundary.Text", "language_service.auth"))

    def test_more_parts_than_files_give_one_part_per_file(self) -> None:
        self.declare("9")

        names, _ = mutation.names(self.root)

        own = [name for name in names if LOGGING in name]
        self.assertEqual((len(own), own[-1]), (len(FILES), f"{LOGGING}@5/5"))

    def test_an_area_with_no_code_stays_one_job_whatever_its_line_says(self) -> None:
        self.declare("4", "Core.Boundary.Text")

        names, _ = mutation.names(self.root)

        self.assertIn("Core.Boundary.Text", names)

    def test_a_service_area_stays_one_job_whatever_its_line_says(self) -> None:
        self.declare("2", "language_service.auth")

        names, _ = mutation.names(self.root)

        self.assertIn("language_service.auth", names)

    def test_the_parts_hold_every_file_once_and_none_of_the_area_inside(self) -> None:
        self.declare("3")

        found = [pattern for number in (1, 2, 3) for pattern in self.patterns(number, 3)]

        self.assertEqual(sorted(found), sorted(f"Logging/{name}.cs" for name in FILES))
        self.assertNotIn(HIDDEN, found)

    def test_a_part_is_dealt_in_turn_from_the_files_sorted_by_path(self) -> None:
        self.declare("3")

        self.assertEqual(self.patterns(2, 3), ("Logging/Beta.cs", "Logging/Gamma.cs"))


def whole(*names: str) -> list[Slice]:
    """Files that are one slice each."""
    return [Slice(name, 0, UNBOUNDED) for name in names]


class SlicedAreaTests(PartsFixture):
    """The area whose biggest file has many members: the unit of the split is the slice (#585)."""

    BIG = "Logging/Big.cs"
    METHODS = 7

    def setUp(self) -> None:
        super().setUp()
        body = "".join(f"    public int M{n}() {{ return {n}; }}\n" for n in range(self.METHODS))
        head = "namespace Core.Platform.Logging;\n\npublic sealed class Big\n{\n"
        text = head + body + "}\n"
        support.write(self.root, f"core/src/Core.Platform/{self.BIG}", text)
        self.text = text
        self.declare("3")

    def spans(self, number: int) -> list[tuple[int, int]]:
        """The spans of the big file in the part, as the patterns give them."""
        found = [p for p in self.patterns(number, 3) if p.startswith(self.BIG)]
        spans = re.findall(r"\{(\d+)\.\.(\d+)\}", "".join(found))
        return [(int(a), int(b)) for a, b in spans]

    def test_the_big_file_is_in_every_part_each_with_its_own_members(self) -> None:
        for number in (1, 2, 3):
            self.assertTrue(self.spans(number), number)

    def test_the_spans_of_the_parts_tile_the_file_with_no_gap_and_no_overlap(self) -> None:
        spans = sorted(span for number in (1, 2, 3) for span in self.spans(number))

        self.assertEqual(spans[0][0], 0)
        self.assertEqual(spans[-1][1], UNBOUNDED)
        self.assertTrue(all(a[1] == b[0] for a, b in itertools.pairwise(spans)))

    def test_no_span_ends_inside_a_member(self) -> None:
        ends = {
            end for _, end in (span for n in (1, 2, 3) for span in self.spans(n)) if end < UNBOUNDED
        }

        for end in ends:
            self.assertEqual(self.text[end - 1], "}")

    def test_the_whole_files_stay_bare_paths_beside_the_spans_of_the_big_one(self) -> None:
        found = [p for n in (1, 2, 3) for p in self.patterns(n, 3) if "{" not in p]

        self.assertEqual(sorted(found), sorted(f"Logging/{name}.cs" for name in FILES))

    def test_every_pattern_names_a_file_of_the_area_and_none_of_the_area_inside(self) -> None:
        files = {f"Logging/{name}.cs" for name in (*FILES, "Big")}

        for number in (1, 2, 3):
            named = {re.split(r"[{]", pattern)[0] for pattern in self.patterns(number, 3)}

            self.assertTrue(named <= files, (number, named - files))
            self.assertNotIn(HIDDEN, named)

    def test_the_parts_together_name_every_file_of_the_area(self) -> None:
        files = {f"Logging/{name}.cs" for name in (*FILES, "Big")}

        named = {re.split(r"[{]", p)[0] for n in (1, 2, 3) for p in self.patterns(n, 3)}

        self.assertEqual(named, files)

    def test_the_cap_of_the_parts_is_the_number_of_slices_not_of_files(self) -> None:
        self.declare("99")

        names, _ = mutation.names(self.root)

        own = [name for name in names if LOGGING in name]
        self.assertEqual(len(own), len(FILES) + self.METHODS + 1)


class ShareTests(unittest.TestCase):
    def test_the_share_does_not_depend_on_the_order_the_files_come_in(self) -> None:
        files = whole("b.cs", "a.cs", "d.cs", "c.cs")

        first = [mutation_parts.share(files, Part(n, 2)) for n in (1, 2)]
        again = [mutation_parts.share(files[::-1], Part(n, 2)) for n in (1, 2)]

        self.assertEqual(first, again)
        self.assertEqual(first, [("a.cs", "c.cs"), ("b.cs", "d.cs")])

    def test_a_part_beyond_the_slices_holds_none(self) -> None:
        self.assertEqual(mutation_parts.share(whole("a.cs"), Part(2, 3)), ())

    def test_the_slices_of_one_file_are_dealt_to_different_parts(self) -> None:
        pieces = [Slice("a.cs", 0, 10), Slice("a.cs", 10, 20), Slice("a.cs", 20, UNBOUNDED)]

        shares = [mutation_parts.share(pieces, Part(n, 3)) for n in (1, 2, 3)]

        self.assertEqual(
            shares, [("a.cs{0..10}",), ("a.cs{10..20}",), (f"a.cs{{20..{UNBOUNDED}}}",)]
        )

    def test_the_slices_a_part_holds_of_a_file_share_one_pattern_with_a_span_each(self) -> None:
        pieces = [Slice("a.cs", 0, 10), Slice("a.cs", 10, 20), Slice("a.cs", 20, UNBOUNDED)]

        self.assertEqual(
            mutation_parts.share(pieces, Part(1, 2)), (f"a.cs{{0..10}}{{20..{UNBOUNDED}}}",)
        )

    def test_the_heaviest_slice_is_alone_and_the_rest_balance_the_other_parts(self) -> None:
        weights = [100, 30, 30, 30, 10]
        pieces = [Slice("a.cs", n * 10, n * 10 + 10, w) for n, w in enumerate(weights)]

        loads = [sum(p.weight for p in mutation_parts.dealt(pieces, Part(n, 3))) for n in (1, 2, 3)]

        self.assertEqual(loads, [100, 60, 40])
        self.assertEqual([p.weight for p in mutation_parts.dealt(pieces, Part(1, 3))], [100])

    def test_slices_of_equal_weight_are_dealt_in_turn(self) -> None:
        pieces = [Slice("a.cs", n * 10, n * 10 + 10) for n in range(6)]

        starts = [[p.start for p in mutation_parts.dealt(pieces, Part(n, 3))] for n in (1, 2, 3)]

        self.assertEqual(starts, [[0, 30], [10, 40], [20, 50]])

    def test_every_slice_is_in_exactly_one_part_whatever_the_number_of_parts(self) -> None:
        pieces = [Slice(f"{name}.cs", a, a + 7) for name in "bca" for a in range(0, 35, 7)]

        for count in range(1, 9):
            dealt = [
                p for n in range(1, count + 1) for p in mutation_parts.dealt(pieces, Part(n, count))
            ]

            self.assertEqual(sorted(dealt), sorted(pieces), count)

    def test_a_name_with_a_part_splits_back_into_the_prefix_and_the_part(self) -> None:
        name = mutation_parts.module_name("Core.X", Part(2, 3))

        split = mutation_parts.split_module(name)

        self.assertEqual((name, split), ("Core.X@2/3", ("Core.X", Part(2, 3), None)))

    def test_a_name_without_a_part_stays_as_it_is(self) -> None:
        self.assertEqual(mutation_parts.split_module("Core.X"), ("Core.X", None, None))
        self.assertEqual(mutation_parts.split_module(None), (None, None, None))

    def test_a_part_that_is_not_two_positive_integers_is_a_problem(self) -> None:
        for shape in ("x", "1", "0/3", "1/0", "1-3", "1/"):
            _, part, problem = mutation_parts.split_module(f"Core.X@{shape}")

            self.assertEqual((part, problem is not None), (None, True), shape)

    def test_the_part_must_be_one_of_the_parts_the_area_has(self) -> None:
        held = [Part(n, 3).holds(3) for n in (1, 2, 3)]

        self.assertEqual(held, [True, True, True])
        self.assertEqual((Part(4, 3).holds(3), Part(1, 2).holds(3)), (False, False))


class ModuleWithPartTests(PartsFixture):
    def test_a_part_runs_stryker_once_over_only_its_files(self) -> None:
        self.declare("3")
        runner = fixture.ToolRunner()

        result = self.run_step(runner, f"{LOGGING}@1/3")

        self.assertEqual(self.mutated(runner), [["Logging/Alpha.cs", "Logging/Formatter.cs"]])
        self.assertTrue(any("parte 1/3" in line for line in result.details))
        self.assertFalse(runner.called("--max-children"))

    def test_the_module_without_a_part_runs_every_part_in_series(self) -> None:
        self.declare("3")
        runner = fixture.ToolRunner()

        self.run_step(runner, LOGGING)

        self.assertEqual(len(self.mutated(runner)), 3)

    def test_the_scope_names_the_part(self) -> None:
        self.declare("3")

        result = self.run_step(fixture.ToolRunner(), f"{LOGGING}@3/3")

        self.assertTrue(result.details[0].startswith(f"módulo `{LOGGING}@3/3`"), result.details[0])

    def test_an_area_without_the_line_runs_the_whole_area_in_one_call(self) -> None:
        self.declare(None)
        runner = fixture.ToolRunner()

        self.run_step(runner, LOGGING)

        self.assertEqual(self.mutated(runner), [["Logging/**/*.cs", "!Logging/Inner/**/*.cs"]])

    def test_a_part_the_area_does_not_have_fails_without_running_anything(self) -> None:
        self.declare("3")
        for module in (f"{LOGGING}@4/3", f"{LOGGING}@1/2", "language_service.auth@1/2"):
            runner = fixture.ToolRunner()

            result = self.run_step(runner, module)

            self.assertEqual((result.ok, runner.commands), (False, []), module)
            self.assertIn("parte(s)", result.details[0])

    def test_a_malformed_part_fails_without_running_anything(self) -> None:
        runner = fixture.ToolRunner()

        result = self.run_step(runner, f"{LOGGING}@x")

        self.assertEqual((result.ok, runner.commands), (False, []))
        self.assertIn("a parte tem a forma", result.details[0])

    def test_an_unknown_prefix_with_a_part_is_not_a_critical_module(self) -> None:
        result = self.run_step(fixture.ToolRunner(), "Core.Platform@1/2")

        self.assertIn("`Core.Platform@1/2` não é módulo crítico", result.details[0])


class LineOfTheAreaFileTests(PartsFixture):
    def parts_of(self, text: str | None) -> tuple[list[int], tuple[str, ...]]:
        if text is not None:
            support.write(self.root, f"{FOLDER}/{LOGGING}.md", text)
        table = areas_doc.read_areas(self.root)
        return [a.parts for a in table.areas if a.prefix == LOGGING], table.problems

    def test_a_file_without_the_line_has_one_part(self) -> None:
        self.assertEqual(self.parts_of(area_file(LOGGING)), ([1], ()))

    def test_the_line_gives_the_number_of_parts(self) -> None:
        self.assertEqual(self.parts_of(area_file(LOGGING, parts="3")), ([3], ()))

    def test_a_value_that_is_not_an_integer_from_1_to_99_is_a_problem_naming_the_file(self) -> None:
        for value in ("0", "100", "três", "-1", "2,5", ""):
            _, problems = self.parts_of(area_file(LOGGING, parts=value))

            self.assertEqual(len(problems), 1, value)
            self.assertIn(f"{LOGGING}.md: Partes da mutação '{value}'", problems[0])

    def test_the_line_twice_is_a_problem(self) -> None:
        another = "- Partes da mutação: 3\n- Motivo"
        twice = area_file(LOGGING, parts="2").replace("- Motivo", another)

        _, problems = self.parts_of(twice)

        self.assertEqual(len(problems), 1)
        self.assertIn("o campo `Partes da mutação` já aparece antes", problems[0])


class RepositoryAreaTests(unittest.TestCase):
    def test_the_content_area_of_the_repository_runs_in_three_parts(self) -> None:
        root = support.VERIFY_CODE.parents[1]
        areas = {a.prefix: a for a in areas_doc.read_areas(root).areas}

        self.assertEqual(areas["Core.Persistence.Content"].parts, 3)
        self.assertEqual(areas["Core.Persistence.Content.Reading"].parts, 1)

    def test_the_content_area_of_the_repository_has_files_for_its_three_parts(self) -> None:
        root = support.VERIFY_CODE.parents[1]
        names, problems = mutation.names(root)

        content = [name for name in names if name.startswith("Core.Persistence.Content@")]

        expected = [f"Core.Persistence.Content@{n}/3" for n in (1, 2, 3)]
        self.assertEqual((problems, content), ((), expected))


if __name__ == "__main__":
    unittest.main()
