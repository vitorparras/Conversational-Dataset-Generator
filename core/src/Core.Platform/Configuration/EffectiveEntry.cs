namespace Core.Platform.Configuration;

/// <summary>One key in force: its path, the value as it may be shown (null when an optional key has none), and where it came from.</summary>
public sealed record EffectiveEntry(string Key, string? Value, ConfigurationOrigin Origin);
