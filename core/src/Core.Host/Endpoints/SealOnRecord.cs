namespace Core.Host.Endpoints;

/// <summary>What the chain holds of its seal in the manual situation: when the last one was recorded and the warning that tells one is missing or old.</summary>
/// <param name="LastAt">When the chain last recorded a seal; null when it never did.</param>
/// <param name="Warning">The warning in words; null when there is none.</param>
internal sealed record SealOnRecord(DateTimeOffset? LastAt, string? Warning);
