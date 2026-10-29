using Core.Boundary.Contracts;
using Core.Boundary.Http;

namespace Core.Boundary.Tests.Properties;

/// <summary>The validator as a property sees it: the production one, or one that measures the text wrongly on purpose.</summary>
internal sealed record AnswerValidation(Func<AnalysisRequest, ReadOnlyMemory<byte>, ResponseValidation> Validate)
{
    public static AnswerValidation Production { get; } =
        new((request, body) => ResponseValidator.Validate(request, ContractVersion.Current, body));

    /// <summary>Measures the text in UTF-16 units, as if each were a code point.</summary>
    public static AnswerValidation CountingUtf16Units { get; } =
        new((request, body) => Production.Validate(request with { Text = new string('a', request.Text.Length) }, body));

    /// <summary>Reads the end as the last code point inside the span, so an occurrence reaching the end of the text falls out.</summary>
    public static AnswerValidation ReadingTheEndAsInclusive { get; } =
        new((request, body) => Production.Validate(request with { Text = string.Concat(request.Text.EnumerateRunes().SkipLast(1)) }, body));
}
