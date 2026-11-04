namespace Core.Host.Endpoints;

/// <summary>What whoever operates brings to the verification against the seal: the seal in its form in text, which the verification reads before anything runs.</summary>
/// <param name="Seal">The text of the seal, as it was issued.</param>
internal sealed record SealAsked(string Seal);
