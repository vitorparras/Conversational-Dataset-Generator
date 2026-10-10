namespace Core.Platform.Configuration;

/// <summary>The closed set of keys the process accepts, assembled from the modules' sections.</summary>
public sealed class ConfigurationSchema
{
    private readonly Dictionary<string, (string Path, ConfigurationKey Key)> keys =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly List<string> paths = [];

    /// <summary>Assembles the schema. Two declarations of the same key path are a defect.</summary>
    public ConfigurationSchema(IEnumerable<ConfigurationSection> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);
        foreach (var section in sections)
        {
            foreach (var key in section.Keys)
            {
                var path = section.Name + ":" + key.Name;
                if (!keys.TryAdd(path, (path, key)))
                {
                    throw new ArgumentException($"key '{path}' is declared twice", nameof(sections));
                }

                paths.Add(path);
            }
        }
    }

    /// <summary>Every key path, in declaration order.</summary>
    public IReadOnlyList<string> Paths => paths;

    /// <summary>The key declared at <paramref name="path"/>.</summary>
    public ConfigurationKey this[string path] => keys[path].Key;

    /// <summary>Finds a key by path, ignoring case as the configuration system does.</summary>
    /// <returns>The path as declared, or null when no section declares it.</returns>
    public string? FindPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return keys.TryGetValue(path, out var found) ? found.Path : null;
    }
}
