namespace Core.Host.Endpoints;

/// <summary>The seal of the chain as it stands, issued to be printed or copied and kept outside the base; nothing was written.</summary>
/// <param name="Seal">The seal in its form in text.</param>
/// <param name="SealedAt">The moment of the seal.</param>
/// <param name="Events">How many events the chain had.</param>
internal sealed record SealIssued(string Seal, DateTimeOffset SealedAt, long Events);
