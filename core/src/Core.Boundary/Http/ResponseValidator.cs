using System.Text.Json;
using System.Text.Unicode;
using Core.Boundary.Contracts;
using Core.Boundary.Text;

namespace Core.Boundary.Http;

/// <summary>
/// Checks an answer of the language service before the core uses any of it: the contract version, the JSON shape and
/// every offset against the text sent. One problem refuses the whole answer; a refusal is a result, not an exception.
/// </summary>
internal static class ResponseValidator
{
    private static readonly JsonDocumentOptions SingleValue = new() { AllowDuplicateProperties = false };

    /// <summary>The answer to the request, whole and with every occurrence inside the text sent, or the problem that refused it.</summary>
    /// <param name="request">The request the answer is for; its text is what the offsets are checked against.</param>
    /// <param name="declaredContractVersion">The contract version the service declared it speaks.</param>
    /// <param name="body">The body of the answer, as the bytes received, so that a broken encoding is refused rather than replaced.</param>
    /// <returns>The accepted answer, or the problem found first; an end before the start wins over an offset outside the text.</returns>
    /// <exception cref="ArgumentNullException">The request or the declared version is null.</exception>
    internal static ResponseValidation<AnalysisResponse> Validate(AnalysisRequest request, string declaredContractVersion, ReadOnlyMemory<byte> body)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(declaredContractVersion);
        return Judge<AnalysisResponse>(declaredContractVersion, body, response => FirstOffsetProblem(request.Text, response.Occurrences) ?? FirstKindProblem(response.Occurrences));
    }

    /// <summary>The answer for a whole item, every occurrence inside the text it names and every author with one group, or the problem that refused it.</summary>
    /// <param name="request">The request the answer is for; its texts and authors are what the answer is checked against.</param>
    /// <param name="declaredContractVersion">The contract version the service declared it speaks.</param>
    /// <param name="body">The body of the answer, as the bytes received.</param>
    /// <exception cref="ArgumentNullException">The request or the declared version is null.</exception>
    internal static ResponseValidation<ItemAnalysisResponse> ValidateItem(ItemAnalysisRequest request, string declaredContractVersion, ReadOnlyMemory<byte> body)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(declaredContractVersion);
        return Judge<ItemAnalysisResponse>(declaredContractVersion, body, response => ItemAnswerCheck.FirstProblem(request, response));
    }

    private static ResponseValidation<TResponse> Judge<TResponse>(string declaredContractVersion, ReadOnlyMemory<byte> body, Func<TResponse, ValidationProblem?> valueProblem)
        where TResponse : class
    {
        if (!string.Equals(declaredContractVersion, ContractVersion.Current, StringComparison.Ordinal))
        {
            return ResponseValidation<TResponse>.Refused(ValidationProblem.UnknownContractVersion);
        }

        using var document = Parse(body);
        if (document is null)
        {
            return ResponseValidation<TResponse>.Refused(ValidationProblem.MalformedJson);
        }

        var shape = ContractShape.FirstProblem(document.RootElement, typeof(TResponse));
        if (shape is not null)
        {
            return ResponseValidation<TResponse>.Refused(shape.Value);
        }

        if (Deserialize(document, typeof(TResponse)) is not TResponse response)
        {
            return ResponseValidation<TResponse>.Refused(ValidationProblem.WrongValue);
        }

        var problem = valueProblem(response);
        return problem is null ? ResponseValidation<TResponse>.Accepted(response) : ResponseValidation<TResponse>.Refused(problem.Value);
    }

    private static JsonDocument? Parse(ReadOnlyMemory<byte> body)
    {
        if (!Utf8.IsValid(body.Span))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(body, SingleValue);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The answer as the contract reads it; null when a value does not fit the type the contract declares.</summary>
    private static object? Deserialize(JsonDocument document, Type contract)
    {
        try
        {
            return document.Deserialize(contract, ContractJson.Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The first occurrence that does not fit the text; the length in code points comes from the converter, never from counting here.</summary>
    private static ValidationProblem? FirstOffsetProblem(string text, IEnumerable<Occurrence> occurrences)
    {
        var length = OffsetConverter.ToCodePointOffset(text, text.Length);
        return occurrences.Select(occurrence => OffsetProblem(occurrence, length)).FirstOrDefault(problem => problem is not null);
    }

    /// <summary>The first occurrence whose kind is not one of the closed list of the contract, written exactly as the list writes it.</summary>
    private static ValidationProblem? FirstKindProblem(IEnumerable<Occurrence> occurrences) =>
        occurrences.Any(occurrence => !OccurrenceKinds.IsKnown(occurrence.Kind)) ? ValidationProblem.KindOutsideVocabulary : null;

    private static ValidationProblem? OffsetProblem(Occurrence occurrence, int length)
    {
        if (occurrence.End < occurrence.Start)
        {
            return ValidationProblem.EndBeforeStart;
        }

        return occurrence.Start < 0 || occurrence.End > length ? ValidationProblem.OffsetOutsideText : null;
    }
}
