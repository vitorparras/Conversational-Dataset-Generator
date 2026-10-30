using CsCheck;

namespace Core.Boundary.Tests.Properties;

/// <summary>Texts where code points and UTF-16 units part ways, the empty one included, and positions in them.</summary>
internal static class TextGenerators
{
    private const int MaxPieces = 24;

    /// <summary>Emoji, astral characters, a combining sequence, a ZWJ sequence, a flag, a skin tone and a variation selector, among plain text.</summary>
    private static readonly Gen<string> Named = Gen.OneOf(
        "a",
        " ",
        "PESSOA_1",
        "\u00E9",
        "e\u0301",
        "\U0001F642",
        "\U0001D400",
        "\U00020BB7",
        "\U0001F469\u200D\U0001F4BB",
        "\U0001F1E7\U0001F1F7",
        "\U0001F44D\U0001F3FD",
        "\u2764\uFE0F");

    private static readonly Gen<string> Astral = Gen.Int[0x10000, 0x10FFFF].Select(char.ConvertFromUtf32);

    private static readonly Gen<string> AnyScalar =
        Gen.OneOf(Gen.Int[0, 0xD7FF], Gen.Int[0xE000, 0x10FFFF]).Select(char.ConvertFromUtf32);

    /// <summary>Well-formed texts of up to two dozen pieces; one in ten is empty.</summary>
    public static readonly Gen<string> Texts = Gen.Frequency(
        (1, Gen.Const(string.Empty)),
        (9, Gen.OneOf(Named, AnyScalar).List[1, MaxPieces].Select(string.Concat)));

    /// <summary>A text and a code point offset from its start to its end, both included.</summary>
    public static readonly Gen<PositionCase> CodePointOffsets =
        Texts.SelectMany(text => Gen.Int[0, CodePointCount(text)].Select(offset => new PositionCase(text, offset)));

    /// <summary>A text and a half-open span of it in code points, possibly empty.</summary>
    public static readonly Gen<SpanCase> Spans =
        Texts.SelectMany(text => Gen.Select(Gen.Int[0, CodePointCount(text)], Gen.Int[0, CodePointCount(text)], (first, second) =>
            new SpanCase(text, Math.Min(first, second), Math.Max(first, second))));

    /// <summary>A text with an astral character somewhere, and the UTF-16 index between the two halves of its pair.</summary>
    public static readonly Gen<PositionCase> IndexesInsidePairs =
        Gen.Select(Texts, Astral, Texts, (before, astral, after) => new PositionCase(before + astral + after, before.Length + 1));

    /// <summary>How the language service counts: one per code point.</summary>
    public static int CodePointCount(string text) => text.EnumerateRunes().Count();
}
