namespace Core.Platform.Configuration;

/// <summary>The closed set of keys the process accepts, united from the sections every module declares.</summary>
public sealed class ConfigurationSchema
{
    private readonly Dictionary<string, DeclaredKey> byPath = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Unites the sections; the same key declared twice is a defect.</summary>
    public ConfigurationSchema(IReadOnlyList<ConfigurationSection> sections)
    {
        Declared = [.. sections.SelectMany(section => section.Keys
            .Select(key => new DeclaredKey($"{section.Name}:{key.Name}", key)))];
        foreach (var declared in Declared)
        {
            Register(declared, nameof(sections));
        }
    }

    /// <summary>Every declared key, in declaration order.</summary>
    internal IReadOnlyList<DeclaredKey> Declared { get; }

    /// <summary>The key declared at the path, whatever the case it was written in; null when no section declares it.</summary>
    internal DeclaredKey? Find(string path) => byPath.GetValueOrDefault(path);

    private void Register(DeclaredKey entry, string parameterName)
    {
        if (!byPath.TryAdd(entry.Path, entry))
        {
            throw new ArgumentException($"Key '{entry.Path}' is declared twice.", parameterName);
        }
    }
}
