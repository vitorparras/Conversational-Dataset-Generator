namespace Core.Persistence.Content.Rotation;

/// <summary>How many keys of a domain are under the newest generation not revoked, and how many are behind it.</summary>
public sealed record RewrapStatus(int Current, int Behind);
