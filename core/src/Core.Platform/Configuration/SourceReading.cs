namespace Core.Platform.Configuration;

/// <summary>What one source handed over: where it read, every key it wrote, and every problem it met reading.</summary>
internal sealed record SourceReading(
    ConfigurationOrigin Origin,
    IReadOnlyList<KeyValuePair<string, string?>> Entries,
    IReadOnlyList<ConfigurationProblem> Problems)
{
    public static SourceReading Of(ConfigurationOrigin origin, IEnumerable<KeyValuePair<string, string?>> entries) =>
        new(origin, [.. entries], []);

    /// <summary>A source that could not be read writes nothing and reports why.</summary>
    public static SourceReading Unreadable(ConfigurationOrigin origin, ConfigurationProblem problem) =>
        new(origin, [], [problem]);
}
