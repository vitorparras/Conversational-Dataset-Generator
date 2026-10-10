namespace Core.Platform.Configuration;

/// <summary>One key of the configuration in force, as it may be shown.</summary>
/// <param name="Key">The key path as its section declares it.</param>
/// <param name="Value">The value in force; null when the key is sensitive.</param>
/// <param name="Origin">The source whose value won.</param>
/// <param name="Redacted">Whether the value was withheld because the key is sensitive.</param>
public sealed record EffectiveConfigurationEntry(
    string Key,
    string? Value,
    ConfigurationOrigin Origin,
    bool Redacted);

/// <summary>The configuration in force: every declared key, its value and the origin that won.</summary>
public sealed class EffectiveConfiguration
{
    private readonly ConfigurationSchema schema;
    private readonly Dictionary<string, (string Value, ConfigurationOrigin Origin)> values;

    internal EffectiveConfiguration(
        ConfigurationSchema schema,
        Dictionary<string, (string Value, ConfigurationOrigin Origin)> values)
    {
        this.schema = schema;
        this.values = values;
    }

    /// <summary>The value in force for <paramref name="path"/>, sensitive or not.</summary>
    /// <remarks>For the module that owns the key. Showing the configuration goes through <see cref="View"/>.</remarks>
    public string Get(string path)
    {
        var declared = schema.FindPath(path)
            ?? throw new ArgumentException($"key '{path}' is not declared", nameof(path));
        return values[declared].Value;
    }

    /// <summary>The origin of the value in force for <paramref name="path"/>.</summary>
    public ConfigurationOrigin OriginOf(string path)
    {
        var declared = schema.FindPath(path)
            ?? throw new ArgumentException($"key '{path}' is not declared", nameof(path));
        return values[declared].Origin;
    }

    /// <summary>The whole configuration, in declaration order, with sensitive values withheld.</summary>
    public IReadOnlyList<EffectiveConfigurationEntry> View() =>
        [.. schema.Paths.Select(path =>
        {
            var (value, origin) = values[path];
            var sensitive = schema[path].Sensitive;
            return new EffectiveConfigurationEntry(path, sensitive ? null : value, origin, sensitive);
        })];
}
