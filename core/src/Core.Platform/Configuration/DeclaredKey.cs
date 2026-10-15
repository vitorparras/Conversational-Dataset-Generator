namespace Core.Platform.Configuration;

/// <summary>A key under its section, addressed by the path a source writes: <c>Section:Key</c>.</summary>
internal sealed record DeclaredKey(string Path, ConfigurationKey Key);
