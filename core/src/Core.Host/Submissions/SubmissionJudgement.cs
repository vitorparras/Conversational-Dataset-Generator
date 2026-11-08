using Core.Ingestion;
using Core.Ingestion.Entrance;

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
