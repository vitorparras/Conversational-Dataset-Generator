using System.Collections;

namespace Core.Platform.Configuration.Sources;

/// <summary>Reads the variables that carry the fixed prefix, <c>__</c> standing for the section separator.</summary>
internal sealed class EnvironmentSource : IConfigurationSource
{
    /// <summary>The prefix every variable the process reads must carry; matched whatever its case.</summary>
    public const string Prefix = "CDG_";
    private const string SectionSeparator = "__";
    private const string PathSeparator = ":";

    private readonly IReadOnlyList<KeyValuePair<string, string?>> variables;

    public EnvironmentSource(IEnumerable<KeyValuePair<string, string?>> variables)
    {
        this.variables = [.. variables];
    }

    /// <summary>The source over the variables of the running process.</summary>
    public static EnvironmentSource FromProcess() =>
        new(Environment.GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .Select(entry => KeyValuePair.Create((string)entry.Key, (string?)entry.Value)));

    public SourceReading Read() =>
        SourceReading.Of(
            ConfigurationOrigin.Environment,
            variables
                .Where(variable => variable.Key.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(variable => variable.Key, StringComparer.Ordinal)
                .Select(variable => KeyValuePair.Create(PathOf(variable.Key), variable.Value)));

    private static string PathOf(string variableName) =>
        variableName[Prefix.Length..].Replace(SectionSeparator, PathSeparator, StringComparison.Ordinal);
}
