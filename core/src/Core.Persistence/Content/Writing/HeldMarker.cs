namespace Core.Persistence.Content.Writing;

/// <summary>A marker an item holds, and whether it stands for an author; the marker keeps nothing of the original and is stored in clear.</summary>
/// <param name="Marker">The marker, as PESSOA_00000001.</param>
/// <param name="Author">Whether it stands for an author of the conversation.</param>
public sealed record HeldMarker(string Marker, bool Author);
