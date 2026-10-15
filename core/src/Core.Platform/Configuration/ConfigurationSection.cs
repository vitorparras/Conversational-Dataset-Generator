namespace Core.Platform.Configuration;

/// <summary>The keys one module declares; a key outside every declared section is unknown.</summary>
internal sealed record ConfigurationSection
{
    public ConfigurationSection(string name, IReadOnlyList<ConfigurationKey> keys)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Contains(':', StringComparison.Ordinal))
        {
            throw new ArgumentException($"Section name '{name}' must not contain ':'.", nameof(name));
        }

        Name = name;
        Keys = [.. keys];
    }

    public string Name { get; }

    public IReadOnlyList<ConfigurationKey> Keys { get; }
}
