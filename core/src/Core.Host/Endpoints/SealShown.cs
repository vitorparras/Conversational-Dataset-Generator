using Core.Persistence.Custody.Verification;

namespace Core.Host.Endpoints;

/// <summary>
/// What one answer of the screen of the seal adds to what the chain holds: the seal just issued, what was typed to be recorded and why it was refused, and whether a record was just done.
/// </summary>
internal sealed class SealShown
{
    /// <summary>The seal just issued, shown once on the answer that issued it; null when none was.</summary>
    internal ChainSeal? Issued { get; init; }

    /// <summary>The text typed to be recorded, given back as typed when it was refused.</summary>
    internal string? Typed { get; init; }

    /// <summary>Why the text typed was not recorded, in words; null when it was not refused.</summary>
    internal string? Refusal { get; init; }

    /// <summary>Whether the chain could not be read for what the answer did.</summary>
    internal bool Unreadable { get; init; }

    /// <summary>Whether the page follows a seal that was recorded.</summary>
    internal bool Recorded { get; init; }
}
