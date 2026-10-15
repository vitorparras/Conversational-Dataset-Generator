namespace Core.Platform.Configuration;

/// <summary>The closed set of keys the process accepts, united from the sections every module declares.</summary>
internal sealed class ConfigurationSchema
{
    private readonly Dictionary<string, DeclaredKey> byPath = new(StringComparer.OrdinalIgnoreCase);

    public ConfigurationSchema(IReadOnlyList<ConfigurationSection> sections)
    {
        Declared = [.. sections.SelectMany(section => section.Keys
            .Select(key => new DeclaredKey($"{section.Name}:{key.Name}", key)))];
        foreach (var declared in Declared)
        {
            if (!byPath.TryAdd(declared.Path, declared))
            {
                throw new ArgumentException($"Key '{declared.Path}' is declared twice.", nameof(sections));
            }
        }
    }

    /// <summary>Every declared key, in declaration order.</summary>
    public IReadOnlyList<DeclaredKey> Declared { get; }

    /// <summary>The key declared at the path, whatever the case it was written in; null when no section declares it.</summary>
    public DeclaredKey? Find(string path) => byPath.GetValueOrDefault(path);
}
