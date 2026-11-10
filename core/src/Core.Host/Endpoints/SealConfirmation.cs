namespace Core.Host.Endpoints;

/// <summary>
/// The end of the record of a seal the person kept: the way it ended and, when it was refused, the kind of the refusal and the reason in words, or what was recorded when it was not.
/// </summary>
/// <param name="Outcome">How the record ended.</param>
/// <param name="Kind">The kind of the refusal, by name; empty when there is none to tell.</param>
/// <param name="Reason">The reason in words, for whoever operates; empty when there is none to tell.</param>
/// <param name="Recorded">What the chain recorded; null when it recorded nothing.</param>
internal sealed record SealConfirmation(SealConfirmationOutcome Outcome, string Kind, string Reason, SealConfirmed? Recorded)
{
    internal static SealConfirmation Done(SealConfirmed recorded) => new(SealConfirmationOutcome.Recorded, string.Empty, string.Empty, recorded);

    internal static SealConfirmation Refused(SealConfirmationOutcome outcome, string kind, string reason) => new(outcome, kind, reason, null);
}
