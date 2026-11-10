namespace Core.Host.Endpoints;

/// <summary>How the record of a seal the person kept ended.</summary>
internal enum SealConfirmationOutcome
{
    Recorded,
    NotASeal,
    NotRecorded,
    NoSession,
    Unreadable,
}
