namespace Core.Platform.Configuration;

/// <summary>Loads a source against a closed schema; an unknown key, a missing required key or a refused value refuses the whole configuration.</summary>
internal static class ClosedConfiguration
{
    public static ConfigurationLoadResult Load(ConfigurationSchema schema, IConfigurationSource source)
    {
        var entries = source.Read();
        var unknown = entries
            .Where(entry => schema.Find(entry.Key) is null)
            .Select(entry => new ConfigurationProblem(
                ConfigurationProblemKind.UnknownKey, entry.Key, Found: null, Accepted: null));
        var written = Written(schema, entries);
        var judged = schema.Declared.Select(declared => Judge(declared, written)).OfType<ConfigurationProblem>();
        var problems = unknown.Concat(judged).ToList();
        return problems.Count > 0
            ? ConfigurationLoadResult.Refusal(problems)
            : ConfigurationLoadResult.Loaded(Values(schema, written));
    }

    /// <summary>What the source wrote for each declared key, under the declared path; a later entry replaces an earlier one.</summary>
    private static Dictionary<string, string?> Written(
        ConfigurationSchema schema, IEnumerable<KeyValuePair<string, string?>> entries)
    {
        var written = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            if (schema.Find(entry.Key) is { } declared)
            {
                written[declared.Path] = entry.Value;
            }
        }

        return written;
    }

    private static ConfigurationProblem? Judge(DeclaredKey declared, Dictionary<string, string?> written)
    {
        if (!written.TryGetValue(declared.Path, out var value))
        {
            return declared.Key.IsRequired ? Problem(ConfigurationProblemKind.MissingKey, declared, found: null) : null;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return Problem(ConfigurationProblemKind.EmptyValue, declared, found: null);
        }

        return declared.Key.Accepts(value)
            ? null
            : Problem(ConfigurationProblemKind.ValueNotAccepted, declared, declared.Key.IsSensitive ? null : value);
    }

    private static ConfigurationProblem Problem(ConfigurationProblemKind kind, DeclaredKey declared, string? found) =>
        new(kind, declared.Path, found, declared.Key.AcceptedValues);

    /// <summary>The written value or, for a key no source wrote, its default; only reached once nothing was refused.</summary>
    private static Dictionary<string, string> Values(ConfigurationSchema schema, Dictionary<string, string?> written) =>
        schema.Declared.ToDictionary(
            declared => declared.Path,
            declared => written.GetValueOrDefault(declared.Path) ?? declared.Key.DefaultValue!,
            StringComparer.OrdinalIgnoreCase);
}
