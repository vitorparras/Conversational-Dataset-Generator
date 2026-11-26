using Core.Boundary.Contracts;
using Core.Ingestion.Pseudonymizing;
using Core.Ingestion.Sanitizing;
using Core.Persistence.Content.Rerun;

namespace Core.Host.Submissions;

/// <summary>
/// The answer of the rerun for a held item (issue 102, F3). An item stored before the coreference gets its first pseudonymized text: the service gives only the groups of the raw text,
/// asked with the texts of its batch and with no author, which the item does not keep, so that the batch improves the groups within its message alone. An occurrence held with no
/// pair in the answer refuses the item with the reason, and a service that does not answer spends a try of it. Every other item is rerun as before.
/// </summary>
internal static class RerunAnswers
{
    /// <summary>The answer for the item, asking the service only for an item given the texts of its batch, which holds no map and whose destinations were decided.</summary>
    /// <param name="work">The rerun of the steps in force and the swap.</param>
    /// <param name="recognizer">The recognizer at the boundary, asked only for such an item.</param>
    /// <param name="held">What the item holds, with the raw texts of its batch when it holds no map.</param>
    /// <param name="cancellationToken">Cancels the analysis.</param>
    internal static async Task<RerunAnswer> OfAsync((SanitizationRerun Steps, PseudonymSwap Swap) work, Func<IRecognizer> recognizer, RerunSource held, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recognizer);
        ArgumentNullException.ThrowIfNull(held);
        if (held.Batch is not { } batch || DecidedItemTexts.Held(held) is not { } stored)
        {
            return RerunAnswer.Of(DecidedItemTexts.Rerun(work.Steps, work.Swap, held));
        }

        var request = new ItemAnalysisRequest([.. batch.Select(text => new ItemText(text, null))], SubmissionAnalysis.Language, Guid.NewGuid().ToString(), ContractVersion.Current);
        var outcome = await recognizer().RecognizeItemAsync(request, cancellationToken).ConfigureAwait(false);
        if (outcome.Response is not { } response)
        {
            return RerunAnswer.Unanswered;
        }

        return OccurrencePairing.Paired(stored, response.Occurrences, held.Position) is { } paired
            ? RerunAnswer.Of(DecidedItemTexts.First(work.Steps, work.Swap, held, paired))
            : new RerunAnswer(null, RerunRefusal.PseudonymWithoutPair);
    }
}
