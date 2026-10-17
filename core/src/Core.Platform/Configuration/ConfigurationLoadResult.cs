using System.Collections.ObjectModel;

namespace Core.Platform.Configuration;

/// <summary>Either the configuration in force or every reason it was refused; never an exception.</summary>
public sealed record ConfigurationLoadResult
{
    private ConfigurationLoadResult(
        IReadOnlyDictionary<string, string> values,
        EffectiveConfiguration view,
        IReadOnlyList<ConfigurationProblem> problems)
    {
        Values = values;
        View = view;
        Problems = problems;
    }

    /// <summary>The value of every declared key, under its declared path, as the consumer uses it; empty when refused.</summary>
    public IReadOnlyDictionary<string, string> Values { get; }

    /// <summary>The configuration as it may be shown, with the origin of every value; empty when refused.</summary>
    public EffectiveConfiguration View { get; }

    /// <summary>Every reason for refusal; empty when the configuration loaded.</summary>
    public IReadOnlyList<ConfigurationProblem> Problems { get; }

    /// <summary>Whether anything kept the configuration from loading.</summary>
    public bool Refused => Problems.Count > 0;

    internal static ConfigurationLoadResult Loaded(IReadOnlyList<ResolvedValue> resolved) =>
        new(
            resolved.ToDictionary(value => value.Declared.Path, value => value.Value, StringComparer.OrdinalIgnoreCase),
            new EffectiveConfiguration(resolved),
            []);

    internal static ConfigurationLoadResult Refusal(IReadOnlyList<ConfigurationProblem> problems)
    {
        if (problems.Count == 0)
        {
            throw new ArgumentException("A refusal needs at least one problem.", nameof(problems));
        }

        return new(ReadOnlyDictionary<string, string>.Empty, new EffectiveConfiguration([]), problems);
    }
}
