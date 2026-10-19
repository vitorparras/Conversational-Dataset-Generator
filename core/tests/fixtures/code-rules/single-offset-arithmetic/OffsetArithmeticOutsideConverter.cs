using System.Globalization;
using Core.Boundary.Contracts;
using static System.Char;
using CodePoint = System.Text.Rune;

namespace Fixtures;

internal static class OffsetArithmeticOutsideConverter
{
    public static int Count(string text) => text . EnumerateRunes ( ).Count();

    public static int CountSpan(string text) => text.AsSpan().
        EnumerateRunes().GetEnumerator().Current.Utf16SequenceLength;

    public static bool High(char unit) => IsHighSurrogate(unit);

    public static bool Low(char unit) => char
        .IsLowSurrogate(unit);

    public static bool Pair(string text) => global::System.Char.IsSurrogatePair(text, 0);

    public static int Scalar(string text) => ConvertToUtf32(text, 0);

    public static int First(string text) => CodePoint.GetRuneAt(text, 0).Value;

    public static int Elements(string text) => new StringInfo(text).LengthInTextElements;

    public static TextElementEnumerator Enumerator(string text) => StringInfo.GetTextElementEnumerator(text);

    public static string Cut(string text, Occurrence occurrence) => text.Substring(
        occurrence.Start,
        occurrence.End - occurrence.Start);

    public static string Range(string text, Occurrence occurrence) => text[occurrence.Start ..occurrence.End];

    public static char Head(AnalysisRequest request) => request.Text.AsSpan(1)[0];

    public static string Rest(AnalysisRequest request) => request.Text.Substring(1);

    public static string Sequence(AnalysisRequest request, Occurrence occurrence) =>
        string.Concat(request.Text.Skip(occurrence.Start).Take(occurrence.End - occurrence.Start));

    public static char[] Units(string text, Occurrence occurrence) => text.ToCharArray(occurrence.Start, occurrence.End - occurrence.Start);

    public static char Unit(AnalysisRequest request, Occurrence occurrence) => request.Text.ElementAt(occurrence.Start);

    public static void Copy(string text, Occurrence occurrence, char[] buffer) => text.CopyTo(occurrence.Start, buffer, 0, occurrence.End - occurrence.Start);

    public static string Tail(AnalysisRequest request, Occurrence occurrence) => string.Concat(Enumerable.Skip(request.Text, occurrence.Start));
}
