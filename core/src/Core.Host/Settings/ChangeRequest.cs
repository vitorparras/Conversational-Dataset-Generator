namespace Core.Host.Settings;

/// <summary>What the screen submits: the key, the new value, the code of the motive and who submits, from the session.</summary>
internal sealed record ChangeRequest(string Key, string Value, string Motive, long AuthorId);
