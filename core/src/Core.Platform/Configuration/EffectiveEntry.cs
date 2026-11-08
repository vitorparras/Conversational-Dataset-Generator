namespace Core.Platform.Configuration;

/// <summary>One key in force: its path, the value as it may be shown (null when an optional key has none), where it came from and, for a key that has one, the note about its value.</summary>
public sealed record EffectiveEntry(string Key, string? Value, ConfigurationOrigin Origin, string? Mark = null);
