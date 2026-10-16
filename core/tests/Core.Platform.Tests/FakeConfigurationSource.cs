using Core.Platform.Configuration;

namespace Core.Platform.Tests;

/// <summary>A test double for the source: it hands over exactly the entries it was given.</summary>
internal sealed class FakeConfigurationSource : IConfigurationSource
{
    private readonly IReadOnlyList<KeyValuePair<string, string?>> entries;

    public FakeConfigurationSource(IEnumerable<KeyValuePair<string, string?>> entries)
    {
        this.entries = [.. entries];
    }

    public IReadOnlyList<KeyValuePair<string, string?>> Read() => entries;
}
