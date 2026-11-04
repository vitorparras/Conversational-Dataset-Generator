namespace Core.Host;

/// <summary>
/// The warning of the manual situation of the seal, written by the periodic verification and by the record of a seal and read by /status: no seal ever recorded, or the last older than 7 days.
/// The warning is replaced whole, so a reader never sees half of one; null is no warning.
/// </summary>
internal sealed class SealWarningState
{
    private volatile string? current;

    internal string? Current => current;

    /// <summary>Takes the warning as the current one; null clears it.</summary>
    /// <param name="warning">The warning, or null when there is none.</param>
    internal void Record(string? warning) => current = warning;
}
