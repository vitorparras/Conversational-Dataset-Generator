using Core.Boundary.Contracts;
using CsCheck;

namespace Core.Tests.Properties.Boundary;

/// <summary>Answers about generated texts: every occurrence inside the text, or all inside but one that falls outside it.</summary>
internal static class AnswerGenerators
{
    private const int MaxOccurrences = 4;
    private const int MaxBefore = 3;

    /// <summary>A text and up to four occurrences, each a half-open span of its code points.</summary>
    public static readonly Gen<AnswerCase> Inside =
        TextGenerators.Texts.SelectMany(text => SpansOf(text).List[0, MaxOccurrences].Select(found => new AnswerCase(text, found)));

    /// <summary>
    /// An answer with one occurrence that starts before the text or ends past it; an end past the code points but within the
    /// UTF-16 units is where the two counts part ways.
    /// </summary>
    public static readonly Gen<AnswerCase> OneOutside =
        Inside.SelectMany(answer => Gen.Select(Outside(answer.Text), Gen.Int[0, answer.Occurrences.Count], (outside, at) =>
            answer with { Occurrences = [.. answer.Occurrences.Take(at), outside, .. answer.Occurrences.Skip(at)] }));

    private static Gen<Occurrence> SpansOf(string text)
    {
        var count = TextGenerators.CodePointCount(text);
        return Gen.Select(Gen.Int[0, count], Gen.Int[0, count], (first, second) => At(Math.Min(first, second), Math.Max(first, second)));
    }

    private static Gen<Occurrence> Outside(string text)
    {
        var count = TextGenerators.CodePointCount(text);
        var startsBefore = Gen.Select(Gen.Int[-MaxBefore, -1], Gen.Int[0, count], At);
        var endsPast = Gen.Select(Gen.Int[0, count], Gen.Int[count + 1, text.Length + 1], At);
        return Gen.OneOf(startsBefore, endsPast);
    }

    private static Occurrence At(int start, int end) => new(start, end, "PERSON", "PROPN", "obl");
}
