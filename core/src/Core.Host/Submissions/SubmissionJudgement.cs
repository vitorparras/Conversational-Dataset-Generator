using Core.Ingestion;
using Core.Ingestion.Entrance;
using Core.Persistence.Ingestion;

namespace Core.Host.Submissions;

/// <summary>Why a file received is turned back, if it is: the barrier first, then the conference of the material against the form declared.</summary>
internal static class SubmissionJudgement
{
    /// <summary>The reason the barrier or the conference gives, or null when the file holds up to both.</summary>
    /// <param name="intake">The conference of the forms.</param>
    /// <param name="received">What the barrier answered.</param>
    /// <param name="form">The form the person declared.</param>
    internal static SubmissionReason? Of(SubmissionIntake intake, SubmissionResult received, MaterialForm form) =>
        Barred(received) ?? (intake.Conference.Confer(form, ReadOutcome.Read(received.Material!)).Refusal is { } refused ? ReasonOf(refused) : null);

    /// <summary>Why the file is turned back, if it is: the barrier and the form, then the key that seals, and last the digest, since a file already accepted in the domain is a repeat.</summary>
    /// <param name="intake">The conference of the forms and the path that seals.</param>
    /// <param name="register">Finds the batch of the domain that accepted the same file.</param>
    /// <param name="request">Where the file goes and the form declared.</param>
    /// <param name="received">What the barrier answered.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    internal static async Task<SubmissionReason?> OfAsync(SubmissionIntake intake, BatchRegister register, SubmissionRequest request, SubmissionResult received, CancellationToken cancellationToken)
    {
        var reason = Of(intake, received, request.DeclaredForm) ?? SubmissionStoring.Unsealed(intake.Sealing);
        if (reason is not null)
        {
            return reason;
        }

        var previous = await register.PreviousAsync(request.DomainId, received.Digest, cancellationToken).ConfigureAwait(false);
        return previous is null ? null : RepeatOf(previous);
    }

    /// <summary>The reason of a file that another batch of the domain accepted first; the batch is none when the one that accepted it cannot be found any more.</summary>
    /// <param name="previous">The batch that accepted the same file.</param>
    internal static SubmissionReason RepeatOf(long? previous) => new(ReasonCodes.AlreadyReceived, [], previous);

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
}
