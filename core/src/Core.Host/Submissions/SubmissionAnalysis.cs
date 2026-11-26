using Core.Boundary.Contracts;
using Core.Ingestion;
using Core.Ingestion.Deciding;

namespace Core.Host.Submissions;

/// <summary>
/// The analysis of a file before any of its items is written: one request for the whole item (issue 102), in Portuguese and under a new opaque reference, with its texts in the order
/// the material gives them and each message with its author, and the destinations decided over the answer, whose groups cover the whole conversation; with no analysis, the whole
/// material is left undecided.
/// </summary>
internal static class SubmissionAnalysis
{
    /// <summary>The language every text of a submission is analysed in: fixed, with no field in the form and no choice by domain.</summary>
    internal const string Language = "pt";

    /// <summary>Asks the recognizer for the whole item and decides the destinations of what it found.</summary>
    /// <param name="recognizer">The recognizer at the boundary.</param>
    /// <param name="material">The material that passed the conference of its form.</param>
    /// <param name="cancellationToken">Cancels the analysis.</param>
    /// <exception cref="OperationCanceledException">The submission was cancelled while the item was analysed.</exception>
    internal static async Task<DestinationResult> DecideAsync(IRecognizer recognizer, Material material, CancellationToken cancellationToken)
    {
        var authors = MaterialTexts.AuthorsOf(material);
        List<ItemText> texts = [.. MaterialTexts.Of(material).Select((text, index) => new ItemText(text, authors[index]))];
        var request = new ItemAnalysisRequest(texts, Language, Guid.NewGuid().ToString(), ContractVersion.Current);
        var outcome = await recognizer.RecognizeItemAsync(request, cancellationToken).ConfigureAwait(false);
        return DecidedMaterial.Decide(material, outcome);
    }
}
