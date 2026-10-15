using System.Collections.ObjectModel;

namespace Core.Platform.Configuration;

/// <summary>Either the values in force or every reason the configuration was refused; never an exception.</summary>
internal sealed record ConfigurationLoadResult
{
    private ConfigurationLoadResult(IReadOnlyDictionary<string, string> values, IReadOnlyList<ConfigurationProblem> problems)
    {
        Values = values;
        Problems = problems;
    }

    /// <summary>The value of every declared key, under its declared path; empty when refused.</summary>
    public IReadOnlyDictionary<string, string> Values { get; }

    /// <summary>Every reason for refusal; empty when the configuration loaded.</summary>
    public IReadOnlyList<ConfigurationProblem> Problems { get; }

    public bool Refused => Problems.Count > 0;

    public static ConfigurationLoadResult Loaded(IReadOnlyDictionary<string, string> values) => new(values, []);

    public static ConfigurationLoadResult Refusal(IReadOnlyList<ConfigurationProblem> problems)
    {
        if (problems.Count == 0)
        {
            throw new ArgumentException("A refusal needs at least one problem.", nameof(problems));
        }

        return new(ReadOnlyDictionary<string, string>.Empty, problems);
    }
}
