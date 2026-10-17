namespace Core.Platform.Configuration;

/// <summary>One key in force: its path, the value as it may be shown, and where it came from.</summary>
internal sealed record EffectiveEntry(string Key, string Value, ConfigurationOrigin Origin);
