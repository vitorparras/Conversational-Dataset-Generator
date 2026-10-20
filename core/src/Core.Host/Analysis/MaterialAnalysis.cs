using System.Collections.Immutable;
using Core.Boundary.Contracts;

namespace Core.Host.Analysis;

/// <summary>
/// Asks the recognizer once for one text, under a new opaque reference the recognizer also writes, and hands back the
/// concluded analysis, the only result anything may keep, or the refusal with its cause, which keeps nothing.
/// A retry is a new analysis that names the reference it resumes, and the log ties the two by their references.
/// </summary>
internal sealed class MaterialAnalysis(IRecognizer recognizer, ILogger<MaterialAnalysis> logger)
{
    /// <exception cref="OperationCanceledException">The caller cancelled; an unavailable service is a refusal instead.</exception>
    internal async Task<AnalysisResult> AnalyzeAsync(AnalysisAsked asked, CancellationToken cancellationToken)
    {
        var reference = Guid.NewGuid();
        if (asked.Resumes is { } interrupted)
        {
            logger.Resumed(reference, interrupted);
        }

        var request = new AnalysisRequest(asked.Text, asked.Language, reference.ToString(), ContractVersion.Current);
        var outcome = await recognizer.RecognizeAsync(request, cancellationToken);
        if (outcome.Response is { } response)
        {
            return AnalysisResult.Of(new ConcludedAnalysis(reference, response.Occurrences.Count, Kinds(response), response.ModelVersion));
        }

        logger.Interrupted(reference, outcome.Reason!.Value);
        return AnalysisResult.Of(RefusedAnalysis.For(reference, outcome.Reason.Value));
    }

    /// <summary>The kind is text the recognizer writes, counted exactly as written.</summary>
    private static ImmutableSortedDictionary<string, int> Kinds(AnalysisResponse response) =>
        response.Occurrences
            .GroupBy(occurrence => occurrence.Kind, StringComparer.Ordinal)
            .ToImmutableSortedDictionary(kind => kind.Key, kind => kind.Count(), StringComparer.Ordinal);
}
