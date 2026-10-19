using System.Text.Json;
using System.Text.Unicode;
using Core.Boundary.Contracts;
using Core.Boundary.Text;

namespace Core.Boundary.Http;

/// <summary>
/// Checks an answer of the language service before the core uses any of it: the contract version, the JSON shape and
/// every offset against the text sent. One problem refuses the whole answer; a refusal is a result, not an exception.
/// </summary>
public static class ResponseValidator
{
    private static readonly JsonDocumentOptions SingleValue = new() { AllowDuplicateProperties = false };

    /// <summary>The answer to the request, whole and with every occurrence inside the text sent, or the problem that refused it.</summary>
    /// <param name="request">The request the answer is for; its text is what the offsets are checked against.</param>
    /// <param name="declaredContractVersion">The contract version the service declared it speaks.</param>
    /// <param name="body">The body of the answer, as the bytes received, so that a broken encoding is refused rather than replaced.</param>
    /// <returns>The accepted answer, or the problem found first; an end before the start wins over an offset outside the text.</returns>
    /// <exception cref="ArgumentNullException">The request or the declared version is null.</exception>
    public static ResponseValidation Validate(AnalysisRequest request, string declaredContractVersion, ReadOnlyMemory<byte> body)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(declaredContractVersion);
        if (!string.Equals(declaredContractVersion, ContractVersion.Current, StringComparison.Ordinal))
        {
            return ResponseValidation.Refused(ValidationProblem.UnknownContractVersion);
        }

        return ValidateBody(request.Text, body);
    }

    private static ResponseValidation ValidateBody(string text, ReadOnlyMemory<byte> body)
    {
        using var document = Parse(body);
        if (document is null)
        {
            return ResponseValidation.Refused(ValidationProblem.MalformedJson);
        }

        var shape = ContractShape.FirstProblem(document.RootElement);
        if (shape is not null)
        {
            return ResponseValidation.Refused(shape.Value);
        }

        var response = Deserialize(document);
        if (response is null)
        {
            return ResponseValidation.Refused(ValidationProblem.WrongValue);
        }

        var offsets = FirstOffsetProblem(text, response.Occurrences);
        return offsets is null ? ResponseValidation.Accepted(response) : ResponseValidation.Refused(offsets.Value);
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
    private static AnalysisResponse? Deserialize(JsonDocument document)
    {
        try
        {
            return document.Deserialize<AnalysisResponse>(ContractJson.Options);
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

    private static ValidationProblem? OffsetProblem(Occurrence occurrence, int length)
    {
        if (occurrence.End < occurrence.Start)
        {
            return ValidationProblem.EndBeforeStart;
        }

        return occurrence.Start < 0 || occurrence.End > length ? ValidationProblem.OffsetOutsideText : null;
    }
}
