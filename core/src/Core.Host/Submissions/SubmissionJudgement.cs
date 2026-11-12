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

    /// <summary>Why the file is turned back before the digest is looked at: the barrier and the form, then the key that seals; none when it holds up to them.</summary>
    /// <param name="intake">The conference of the forms and the path that seals.</param>
    /// <param name="request">Where the file goes and the form declared.</param>
    /// <param name="received">What the barrier answered.</param>
    internal static SubmissionReason? Turned(SubmissionIntake intake, SubmissionRequest request, SubmissionResult received) =>
        Of(intake, received, request.DeclaredForm) ?? SubmissionStoring.Unsealed(intake.Sealing);

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

    /// <summary>The reason of a repeat, found by looking up the batch that accepted the same file.</summary>
    /// <param name="register">Finds the batch of the domain that accepted the same file.</param>
    /// <param name="domainId">The domain of the batch.</param>
    /// <param name="digest">The digest of the file.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    internal static async Task<SubmissionReason> RepeatAsync(BatchRegister register, int domainId, string digest, CancellationToken cancellationToken) =>
        RepeatOf(await register.PreviousAsync(domainId, digest, cancellationToken).ConfigureAwait(false));

    /// <summary>The reason of a repeat, with the batch that accepted the file before.</summary>
    /// <param name="previous">That batch; none when it cannot be found any more.</param>
    private static SubmissionReason RepeatOf(long? previous) => new(ReasonCodes.AlreadyReceived, [], previous);

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
