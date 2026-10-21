using Core.Boundary.Contracts;

namespace Fixtures;

internal static class OffsetArithmeticFree
{
    private const string Marker = "--";

    public static string Key(string argument) => argument[Marker.Length..argument.IndexOf('=', StringComparison.Ordinal)];

    public static string Head(string argument) => argument.Substring(0, 1);

    public static int Size(AnalysisRequest request) => request.Text.Length;

    public static bool Ordered(Occurrence occurrence) => occurrence.Start <= occurrence.End;

    public static bool Blank(char unit) => char.IsWhiteSpace(unit);

    public static string Tail(string argument) => string.Concat(argument.Skip(1));

    public static Occurrence[] AfterFirst(IEnumerable<Occurrence> occurrences) => [.. occurrences.OrderBy(o => o.Start).Skip(1)];

    public static Occurrence NonEmpty(IEnumerable<Occurrence> occurrences) => occurrences.Where(o => o.End > o.Start).ElementAt(0);

    public static ReadOnlySpan<Occurrence> Rest(Occurrence[] occurrences, Occurrence first) => occurrences.AsSpan(first.End);
}
