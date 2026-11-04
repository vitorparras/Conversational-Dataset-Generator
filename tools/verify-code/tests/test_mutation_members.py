"""Tests for the cut of a C# file between the members of its types (#585): where a member ends, what
is never cut and the offsets Stryker counts the spans in."""

from __future__ import annotations

import unittest
from itertools import pairwise
from pathlib import Path

from steps import mutation_members
from steps.mutation_members import UNBOUNDED, member_ends, slices

from . import support

TYPE = """using System;

namespace Core.X;

/// <summary>Doc with { and ; and "quotes".</summary>
public sealed class Box
{
    private const string Sql = "SELECT '{' || ';'";
    private readonly int size;

    public Box(int size)
    {
        this.size = size;
    }

    [Obsolete("x")]
    public int Size => size;

    public int Twice() { return size * 2; }

    public static int Count { get; } = 3;
}
"""


def members(text: str) -> list[str]:
    """The text between one cut and the next, trimmed: what each member holds, to read the cuts."""
    bounds = (0, *member_ends(text))
    return [text[a:b].strip() for a, b in pairwise(bounds)]


class WhereMembersEndTests(unittest.TestCase):
    def test_a_cut_follows_each_field_constructor_property_and_method(self) -> None:
        found = members(TYPE)

        self.assertEqual(len(found), 6)
        self.assertTrue(found[0].endswith("""const string Sql = "SELECT '{' || ';'";"""))
        self.assertEqual(found[1], "private readonly int size;")
        self.assertTrue(found[2].startswith("public Box(int size)") and found[2].endswith("}"))
        self.assertTrue(found[3].startswith('[Obsolete("x")]') and found[3].endswith("size;"))
        self.assertEqual(found[4], "public int Twice() { return size * 2; }")
        self.assertEqual(found[5], "public static int Count { get; } = 3;")

    def test_the_braces_in_comments_strings_and_characters_are_not_read(self) -> None:
        text = 'class A { char a = \'{\'; string b = "}"; /* } */ string c = @"}""{"; // }\n}\n'

        self.assertEqual(members(text)[1], 'string b = "}";')

    def test_the_holes_of_an_interpolated_string_may_hold_strings_and_braces(self) -> None:
        text = 'class A { string a = $"{(x ? "}" : "{")} {{}}"; int b = 1; }\n'

        self.assertEqual(len(member_ends(text)), 2)

    def test_a_raw_string_hides_its_braces(self) -> None:
        text = 'class A { string a = """\n  { "x": ";" }\n  """; int b; }\n'

        self.assertEqual(len(member_ends(text)), 2)

    def test_a_lambda_with_a_body_does_not_end_the_member_that_holds_it(self) -> None:
        text = "class A { Task Run() => Task.Run(() => { Work(); }); int b; }\n"

        self.assertEqual(members(text)[1:], ["int b;"])
        self.assertTrue(members(text)[0].endswith("Task.Run(() => { Work(); });"))

    def test_the_initializer_after_the_accessors_stays_with_its_property(self) -> None:
        text = "class A { int P { get; } = 1; int Q { get; } }\n"

        self.assertEqual(members(text)[1], "int Q { get; }")
        self.assertTrue(members(text)[0].endswith("int P { get; } = 1;"))

    def test_a_nested_type_is_one_member_and_is_never_cut_inside(self) -> None:
        text = "class A { class B { int x; void M() { } } int y; }\n"

        self.assertEqual(len(member_ends(text)), 2)

    def test_a_constraint_that_says_class_does_not_make_a_method_a_type(self) -> None:
        text = "class A { void M<T>() where T : class { Work(); } int b; }\n"

        self.assertEqual(len(member_ends(text)), 2)

    def test_the_members_of_every_type_of_the_file_are_cut(self) -> None:
        text = "namespace N { class A { int a; } class B { int b; } }\n"

        self.assertEqual(len(member_ends(text)), 2)

    def test_a_file_whose_braces_do_not_close_is_never_cut(self) -> None:
        self.assertEqual(member_ends("class A { int a; void M() { }\n"), ())
        self.assertEqual(member_ends("class A { int a; } }\n"), ())

    def test_an_enum_and_a_file_without_types_are_never_cut(self) -> None:
        self.assertEqual(member_ends("enum E { A, B, C }\n"), ())
        self.assertEqual(member_ends("using System;\nConsole.WriteLine(1);\n"), ())


class SlicesTests(unittest.TestCase):
    def test_the_slices_follow_one_another_from_the_first_character_to_the_unbounded_end(
        self,
    ) -> None:
        found = slices(TYPE)

        self.assertEqual((found[0][0], found[-1][1]), (0, UNBOUNDED))
        self.assertTrue(all(a[1] == b[0] for a, b in pairwise(found)))
        self.assertTrue(all(start < end for start, end in found))

    def test_a_file_without_cuts_is_one_slice_of_the_whole_file(self) -> None:
        self.assertEqual(slices("enum E { A }\n"), ((0, UNBOUNDED),))

    def test_the_offsets_count_utf16_units_and_every_character_of_the_line_break(self) -> None:
        text = 'class A { string a = "😀";\r\n int b; }\r\n'

        first_end = slices(text)[0][1]

        self.assertEqual(
            text.encode("utf-16-le")[: first_end * 2].decode("utf-16-le"),
            'class A { string a = "😀";',
        )
        self.assertEqual(first_end, len(text.split("\r\n", maxsplit=1)[0]) + 1)

    def test_each_slice_starts_where_the_member_before_ended(self) -> None:
        pieces = [TYPE[a : min(b, len(TYPE))] for a, b in slices(TYPE)]

        self.assertEqual("".join(pieces), TYPE)
        self.assertEqual(len(pieces), len(member_ends(TYPE)) + 1)

    def test_the_largest_offset_is_the_one_of_a_pattern_without_a_span(self) -> None:
        self.assertEqual(mutation_members.UNBOUNDED, 2147483647)


class RealCodeTests(unittest.TestCase):
    """The cut over every file of core/src, which the step reads as it is."""

    SOURCES = support.VERIFY_CODE.parents[1] / "core/src"

    def files(self) -> list[tuple[Path, str]]:
        found = (p for p in self.SOURCES.rglob("*.cs") if not {"bin", "obj"} & set(p.parts))
        return [(p, p.read_bytes().decode("utf-8-sig")) for p in found]

    def test_the_slices_of_every_file_follow_one_another_and_end_at_a_member(self) -> None:
        for path, text in self.files():
            found = slices(text)

            self.assertEqual((found[0][0], found[-1][1]), (0, UNBOUNDED), path.name)
            self.assertTrue(all(a[1] == b[0] for a, b in pairwise(found)), path.name)
            for end in member_ends(text):
                self.assertIn(text[end - 1], "};", path.name)

    def test_the_braces_of_every_file_close_so_none_is_left_whole_by_a_misreading(self) -> None:
        cut = [path.name for path, text in self.files() if len(slices(text)) > 1]

        self.assertGreater(len(cut), len(self.files()) // 2)
