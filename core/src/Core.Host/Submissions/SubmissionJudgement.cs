using Core.Ingestion;
using Core.Ingestion.Entrance;
using Core.Persistence.Ingestion;

namespace Core.Host.Submissions;

/// <summary>Why a file received is turned back, if it is: the barrier first, then the conference of the material against the form declared.</summary>
internal static class SubmissionJudgement
{
    /// <summary>
    /// Why the file is turned back before the digest is looked at: the barrier and the form, then the key that seals; and the material as the one conference of the form declared
    /// accepts it, for the analysis to read; no reason when it holds up to them all.
    /// </summary>
    /// <param name="intake">The conference of the forms and the path that seals.</param>
    /// <param name="request">Where the file goes and the form declared.</param>
    /// <param name="received">What the barrier answered.</param>
    internal static Judged Turned(SubmissionIntake intake, SubmissionRequest request, SubmissionResult received)
    {
        var judged = Of(intake, received, request.DeclaredForm);
        return judged.Reason is null && SubmissionStoring.Unsealed(intake.Sealing) is { } unsealed ? judged with { Reason = unsealed } : judged;
    }

    /// <summary>What is kept of a file that was not turned back: what the reader gave and, for the analysis to read, the material its one conference accepted.</summary>
    /// <param name="request">Where the file goes and the form declared.</param>
    /// <param name="received">What the barrier answered, read whole.</param>
    /// <param name="batch">The batch the file was opened as.</param>
    /// <param name="judged">The judgement that let the file through, with the material accepted.</param>
    internal static SubmissionStoring.Kept KeptOf(SubmissionRequest request, SubmissionResult received, long batch, Judged judged) =>
        new(request.DomainId, batch, received.Material!, request.DeclaredForm, judged.Accepted!);

    /// <summary>The reason of a repeat when a file already accepted in the domain has the digest; none when no batch accepted it.</summary>
    /// <param name="register">Finds the batch of the domain that accepted the same file.</param>
    /// <param name="domainId">The domain of the batch.</param>
    /// <param name="digest">The digest of the file.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    internal static async Task<SubmissionReason?> PreviousAsync(BatchRegister register, int domainId, string digest, CancellationToken cancellationToken)
    {
        var previous = await register.PreviousAsync(domainId, digest, cancellationToken).ConfigureAwait(false);
        return previous is null ? null : RepeatOf(previous);
    }

    /// <summary>The reason of a repeat, with the batch that accepted the file before.</summary>
    /// <param name="previous">That batch; none when it cannot be found any more.</param>
    private static SubmissionReason RepeatOf(long? previous) => new(ReasonCodes.AlreadyReceived, [], previous);

    private static Judged Of(SubmissionIntake intake, SubmissionResult received, MaterialForm form)
    {
        if (Barred(received) is { } barred)
        {
            return new Judged(barred, null);
        }

        var conferred = intake.Conference.Confer(form, ReadOutcome.Read(received.Material!));
        return new Judged(conferred.Refusal is { } refused ? ReasonOf(refused) : null, conferred.Accepted);
    }

    private static SubmissionReason? Barred(SubmissionResult received)
    {
        var none = new List<SubmissionFinding>();
        return received.Refusal is { } refusal ? new SubmissionReason(ReasonCodes.Of(refusal), none) : null;
    }

    private static SubmissionReason ReasonOf(Refusal refusal)
    {
        var findings = refusal.Findings.Select(finding => new SubmissionFinding(finding.Unit, finding.Field is { } field ? ReasonCodes.Of(field) : null)).ToList();
        return new SubmissionReason(ReasonCodes.Of(refusal.Code), findings);
    }

    /// <summary>What the judgement of a file gives: why it is turned back, if it is, and the material the conference accepted, if it did.</summary>
    /// <param name="Reason">Why the file is turned back; null when it is not.</param>
    /// <param name="Accepted">The material as the conference of the form declared accepts it; null when the barrier or the conference turned it back.</param>
    internal sealed record Judged(SubmissionReason? Reason, Material? Accepted);
}
