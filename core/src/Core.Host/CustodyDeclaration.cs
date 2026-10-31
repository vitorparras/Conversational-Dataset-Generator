using Core.Platform.Logging;

namespace Core.Host;

/// <summary>What a real installation declares about its safekeeping copy, written in the startup line; the holder is a person, so the log erases it and keeps the date.</summary>
/// <param name="LastRecoveryTest">The date the recovery was last tested, as written in ISO form.</param>
/// <param name="Holder">Who holds the copy.</param>
internal sealed record CustodyDeclaration(string LastRecoveryTest, [property: Identity] string Holder);
