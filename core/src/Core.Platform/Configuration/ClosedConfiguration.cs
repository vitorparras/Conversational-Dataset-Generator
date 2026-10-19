namespace Core.Platform.Configuration;

/// <summary>Loads the sources, in order of origin, against a closed schema; an unknown, repeated or missing key or a refused value refuses the whole configuration.</summary>
public static class ClosedConfiguration
{
    /// <summary>Reads every source, a later origin overriding an earlier one, and judges every declared key; nothing throws for what an operator can get wrong.</summary>
    public static ConfigurationLoadResult Load(ConfigurationSchema schema, params IEnumerable<IConfigurationSource> sources)
    {
        ArgumentNullException.ThrowIfNull(schema);
        var readings = sources.Select(source => source.Read()).OrderBy(reading => reading.Origin).ToList();
        var written = Written(schema, readings);
        var problems = readings.SelectMany(reading => Problems(schema, reading))
            .Concat(schema.Declared.Select(declared => Judge(declared, written)).OfType<ConfigurationProblem>())
            .ToList();
        return problems.Count > 0
            ? ConfigurationLoadResult.Refusal(problems)
            : ConfigurationLoadResult.Loaded(Resolve(schema, written));
    }

    /// <summary>What the reading itself reported, then every key it wrote twice, then every key no section declares.</summary>
    private static IEnumerable<ConfigurationProblem> Problems(ConfigurationSchema schema, SourceReading reading) =>
        reading.Problems
            .Concat(reading.Entries
                .GroupBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => Problem(ConfigurationProblemKind.RepeatedKey, group.Key)))
            .Concat(reading.Entries
                .Where(entry => schema.Find(entry.Key) is null)
                .DistinctBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                .Select(entry => Problem(ConfigurationProblemKind.UnknownKey, entry.Key)));

    /// <summary>What was written for each declared key, under the declared path; a later origin replaces an earlier one.</summary>
    private static Dictionary<string, Writing> Written(ConfigurationSchema schema, IEnumerable<SourceReading> readings) =>
        readings
            .SelectMany(reading => reading.Entries
                .Select(entry => KeyValuePair.Create(schema.Find(entry.Key), new Writing(entry.Value, reading.Origin))))
            .Where(pair => pair.Key is not null)
            .GroupBy(pair => pair.Key!.Path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.OrdinalIgnoreCase);

    private static ConfigurationProblem? Judge(DeclaredKey declared, IReadOnlyDictionary<string, Writing> written)
    {
        if (!written.TryGetValue(declared.Path, out var writing))
        {
            return declared.Key.IsRequired ? Problem(ConfigurationProblemKind.MissingKey, declared, found: null) : null;
        }

        if (string.IsNullOrWhiteSpace(writing.Value))
        {
            return Problem(ConfigurationProblemKind.EmptyValue, declared, found: null);
        }

        if (declared.Key.Accepts(writing.Value))
        {
            return null;
        }

        return Problem(ConfigurationProblemKind.ValueNotAccepted, declared, declared.Key.IsSensitive ? null : writing.Value);
    }

    private static ConfigurationProblem Problem(ConfigurationProblemKind kind, string key) =>
        new(kind, key, Found: null, Accepted: null);

    private static ConfigurationProblem Problem(ConfigurationProblemKind kind, DeclaredKey declared, string? found) =>
        new(kind, declared.Path, found, declared.Key.AcceptedValues);

    /// <summary>The written value with its origin or, for a key no source wrote, its default (if any); only reached once nothing was refused.</summary>
    private static List<ResolvedValue> Resolve(ConfigurationSchema schema, IReadOnlyDictionary<string, Writing> written) =>
        [.. schema.Declared.Select(declared => written.TryGetValue(declared.Path, out var writing)
            ? new ResolvedValue(declared, writing.Value, writing.Origin)
            : new ResolvedValue(declared, declared.Key.DefaultValue, ConfigurationOrigin.Default))];

    private sealed record Writing(string? Value, ConfigurationOrigin Origin);
}
