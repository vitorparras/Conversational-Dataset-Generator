using Core.Boundary.Contracts;
using Core.Boundary.Text;

namespace Core.Boundary.Tests.Properties;

/// <summary>The conversion as a property sees it: the production one, or one broken on purpose to show a property can fail.</summary>
internal sealed record OffsetConversion(
    Func<string, int, int> ToUtf16Index,
    Func<string, int, int> ToCodePointOffset,
    Func<string, Occurrence, string> Slice)
{
    public static OffsetConversion Production { get; } =
        new(OffsetConverter.ToUtf16Index, OffsetConverter.ToCodePointOffset, TextSlicer.Slice);

    /// <summary>Takes a code point offset for a UTF-16 index, as if the two counts were the same.</summary>
    public static OffsetConversion CountingUtf16Units { get; } =
        Production with { ToUtf16Index = (_, offset) => offset };

    /// <summary>Moves an index between the halves of a pair back to the start of the pair instead of refusing it.</summary>
    public static OffsetConversion RoundingInsidePairs { get; } =
        Production with { ToCodePointOffset = (text, index) => OffsetConverter.ToCodePointOffset(text, index - (IsInsidePair(text, index) ? 1 : 0)) };

    /// <summary>Cuts the text at the offsets as received, without converting them.</summary>
    public static OffsetConversion SlicingRawOffsets { get; } =
        Production with { Slice = (text, occurrence) => text[occurrence.Start..occurrence.End] };

    private static bool IsInsidePair(string text, int index) =>
        index > 0 && index < text.Length && char.IsSurrogatePair(text[index - 1], text[index]);
}
