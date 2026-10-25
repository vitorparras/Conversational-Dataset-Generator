namespace Core.Platform.Configuration;

/// <summary>A declared key with the value that won and the origin it came from; null for an optional key no source declares.</summary>
internal sealed record ResolvedValue(DeclaredKey Declared, string? Value, ConfigurationOrigin Origin);
