namespace Core.Platform.Configuration;

/// <summary>A source whose entries are given at construction.</summary>
internal sealed class InMemoryConfigurationSource : IConfigurationSource
{
    private readonly IReadOnlyList<KeyValuePair<string, string?>> entries;

    public InMemoryConfigurationSource(IEnumerable<KeyValuePair<string, string?>> entries)
    {
        this.entries = [.. entries];
    }

    public IReadOnlyList<KeyValuePair<string, string?>> Read() => entries;
}
