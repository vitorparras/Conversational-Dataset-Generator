using Microsoft.Extensions.Configuration;

namespace Core.Platform.Configuration;

/// <summary>
/// Loads configuration from a closed set of keys: an unknown key, a missing required key or a
/// value outside what the key accepts refuses the whole configuration (requirement 032).
/// </summary>
public static class ClosedConfiguration
{
    /// <summary>
    /// Reads every source, applies the precedence of <see cref="ConfigurationOrigin"/> and checks
    /// each value against <paramref name="schema"/>. Every problem is reported, not only the first.
    /// </summary>
    public static ConfigurationLoadResult Load(IReadOnlyList<OriginatedSource> sources, ConfigurationSchema schema)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(schema);

        var problems = new List<ConfigurationProblem>();
        var values = new Dictionary<string, (string? Value, ConfigurationOrigin Origin)>(StringComparer.OrdinalIgnoreCase);

        // OrderBy is stable: sources of the same origin keep the order they were given in.
        foreach (var source in sources.OrderBy(s => s.Origin))
        {
            foreach (var (key, value) in Read(source, problems))
            {
                var declared = schema.FindPath(key);
                if (declared is null)
                {
                    // An empty or null declared section declares no key; anything else is unknown,
                    // whatever its value: null and {} are a typo just as much as a string is.
                    if (value is null && schema.IsSection(key))
                    {
                        continue;
                    }

                    problems.Add(new(ConfigurationProblemKind.UnknownKey, key, source.Origin, null, null));
                    continue;
                }

                // A known key written with null (or {}) is declared with a value it does not accept,
                // not left undeclared: null is never the default (requirement 032 CA-3).
                values[declared] = (value, source.Origin);
            }
        }

        foreach (var path in schema.Paths)
        {
            var key = schema[path];
            if (!values.TryGetValue(path, out var found))
            {
                if (key.DefaultValue is null)
                {
                    problems.Add(new(ConfigurationProblemKind.MissingKey, path, null, null, null));
                    continue;
                }

                values[path] = (key.DefaultValue, ConfigurationOrigin.Default);
                continue;
            }

            if (found.Value is null)
            {
                problems.Add(new(ConfigurationProblemKind.ValueNotAccepted, path, found.Origin, "null", key.Accepts));
                continue;
            }

            var accepted = key.Check(found.Value);
            if (accepted is not null)
            {
                var shown = key.Sensitive ? null : found.Value;
                problems.Add(new(ConfigurationProblemKind.ValueNotAccepted, path, found.Origin, shown, accepted));
            }
        }

        return problems.Count > 0
            ? ConfigurationLoadResult.Refusal(problems)
            : ConfigurationLoadResult.Loaded(new EffectiveConfiguration(
                schema,
                values.ToDictionary(pair => pair.Key, pair => (pair.Value.Value!, pair.Value.Origin), StringComparer.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Every key the source writes, as a leaf: a value, or null for <c>null</c> and <c>{}</c>.
    /// A node with children is not returned; its children are, each under its full path.
    /// </summary>
    private static List<KeyValuePair<string, string?>> Read(OriginatedSource source, List<ConfigurationProblem> problems)
    {
        try
        {
            var root = new ConfigurationBuilder().Add(source.Source).Build();
            try
            {
                return [.. root.AsEnumerable()
                    .Where(pair => pair.Value is not null || !root.GetSection(pair.Key).GetChildren().Any())];
            }
            finally
            {
                (root as IDisposable)?.Dispose();
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or FormatException)
        {
            var reason = exception is FormatException ? exception.Message : "o conteúdo não é JSON válido";
            problems.Add(new(ConfigurationProblemKind.UnreadableSource, source.Name, source.Origin, reason, null));
            return [];
        }
    }
}
