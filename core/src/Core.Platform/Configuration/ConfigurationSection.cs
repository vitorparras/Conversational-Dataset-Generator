namespace Core.Platform.Configuration;

/// <summary>The keys one module declares. A key outside every declared section is unknown.</summary>
public sealed record ConfigurationSection
{
    /// <summary>Declares a section and its keys.</summary>
    public ConfigurationSection(string name, IReadOnlyList<ConfigurationKey> keys)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(keys);
        Name = name;
        Keys = keys;
    }

    /// <summary>The section name, the first segment of every key path in it.</summary>
    public string Name { get; }

    /// <summary>The keys the section accepts.</summary>
    public IReadOnlyList<ConfigurationKey> Keys { get; }
}
