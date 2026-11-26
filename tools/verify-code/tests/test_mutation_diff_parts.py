"""Tests for the mutation of the diff in a module that runs in parts: the list and the step give the
files of the diff the same share of the parts, and a part of the whole module is refused."""

from __future__ import annotations

from steps import areas_doc, mutation
from steps.common import OUTPUT_DIRECTORY, Context
from steps.mutation_scope import read

from . import support
from .test_mutation_diff import runner
from .test_mutation_parts import FILES, LOGGING, PartsFixture

CHANGED = ("Alpha", "Beta")
#: A file with code Stryker could mutate; ``class C {}`` holds none.
CODE = "class C { int f = 0; }\n"


def write(fixture: PartsFixture, name: str, text: str) -> None:
    support.write(fixture.root, f"core/src/Core.Platform/Logging/{name}.cs", text)


class DiffPartsTests(PartsFixture):
    def setUp(self) -> None:
        super().setUp()
        self.declare("4")
        for name in FILES:
            write(self, name, CODE)
        self.tools = runner(*(f"core/src/Core.Platform/Logging/{name}.cs" for name in CHANGED))

    def context(self, module: str | None = None) -> Context:
        out = self.root / OUTPUT_DIRECTORY
        return Context(self.root, out, self.tools, "origin/main", None, self.tools, module)

    def test_the_list_gives_two_of_five_equal_files_two_of_the_four_parts(self) -> None:
        every = areas_doc.read_areas(self.root).areas

        names = mutation.jobs(self.root, self.area(), every, read(self.context()))

        self.assertEqual(names, (f"{LOGGING}@1/2", f"{LOGGING}@2/2"))

    def test_each_part_the_list_names_mutates_one_file_of_the_diff(self) -> None:
        mutated: list[list[str]] = []
        for part in ("1/2", "2/2"):
            result = mutation.run(self.context(f"{LOGGING}@{part}"))
            self.assertTrue(result.ok, result.details)
            mutated.append(self.mutated(self.tools)[-1])

        self.assertEqual(sorted(mutated), [["Logging/Alpha.cs"], ["Logging/Beta.cs"]])

    def test_a_part_of_the_whole_module_is_refused_in_a_run_of_the_diff(self) -> None:
        result = mutation.run(self.context(f"{LOGGING}@3/4"))

        self.assertFalse(result.ok)
        self.assertIn(f"`{LOGGING}` roda em 2 parte(s)", "\n".join(result.details))
        self.assertEqual(self.tools.called("stryker"), [])


class IdlePartTests(PartsFixture):
    """A part of the diff with no code to mutate runs no suite (#935)."""

    def setUp(self) -> None:
        super().setUp()
        self.declare("4")
        write(self, "Alpha", CODE)
        self.tools = runner(*(f"core/src/Core.Platform/Logging/{name}.cs" for name in CHANGED))

    def context(self, module: str | None = None) -> Context:
        out = self.root / OUTPUT_DIRECTORY
        return Context(self.root, out, self.tools, "origin/main", None, self.tools, module)

    def test_the_list_leaves_out_the_parts_of_the_diff_with_no_code_to_mutate(self) -> None:
        every = areas_doc.read_areas(self.root).areas

        names = mutation.jobs(self.root, self.area(), every, read(self.context()))

        self.assertEqual(names, (f"{LOGGING}@1/2",))

    def test_a_part_with_no_code_to_mutate_passes_without_running_the_suite(self) -> None:
        result = mutation.run(self.context(f"{LOGGING}@2/2"))

        self.assertTrue(result.ok, result.details)
        self.assertIn("a suíte não roda", "\n".join(result.details))
        self.assertEqual((self.tools.called("stryker"), self.tools.called("restore")), ([], []))

    def test_the_part_with_code_still_runs_the_suite(self) -> None:
        result = mutation.run(self.context(f"{LOGGING}@1/2"))

        self.assertTrue(result.ok, result.details)
        self.assertEqual(self.mutated(self.tools), [["Logging/Alpha.cs{0..20}"]])

    def test_the_whole_module_in_the_nightly_run_keeps_every_part(self) -> None:
        every = areas_doc.read_areas(self.root).areas

        names = mutation.jobs(self.root, self.area(), every)

        self.assertEqual(names, tuple(f"{LOGGING}@{n}/4" for n in range(1, 5)))
