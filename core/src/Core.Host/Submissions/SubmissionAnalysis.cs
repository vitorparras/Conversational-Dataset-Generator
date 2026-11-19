using Core.Boundary.Contracts;
using Core.Ingestion;
using Core.Ingestion.Deciding;

namespace Core.Host.Submissions;

/// <summary>
/// The analysis of a file before any of its items is written: one request per text of the material, in the order the material gives them, each in Portuguese and under a new
/// opaque reference, and the destinations decided over every answer; a text with no analysis leaves the whole material undecided.
/// </summary>
internal static class SubmissionAnalysis
{
    /// <summary>The language every text of a submission is analysed in: fixed, with no field in the form and no choice by domain.</summary>
    internal const string Language = "pt";

    /// <summary>Asks the recognizer for every text of the material and decides the destinations of what it found.</summary>
    /// <param name="recognizer">The recognizer at the boundary.</param>
    /// <param name="material">The material that passed the conference of its form.</param>
    /// <param name="cancellationToken">Cancels the analysis.</param>
    /// <exception cref="OperationCanceledException">The submission was cancelled while a text was analysed.</exception>
    internal static async Task<DestinationResult> DecideAsync(IRecognizer recognizer, Material material, CancellationToken cancellationToken)
    {
        var outcomes = new List<RecognitionOutcome>();
        foreach (var text in MaterialTexts.Of(material))
        {
            var request = new AnalysisRequest(text, Language, Guid.NewGuid().ToString(), ContractVersion.Current);
            outcomes.Add(await recognizer.RecognizeAsync(request, cancellationToken).ConfigureAwait(false));
        }

        return DecidedMaterial.Decide(material, outcomes);
    }
}
