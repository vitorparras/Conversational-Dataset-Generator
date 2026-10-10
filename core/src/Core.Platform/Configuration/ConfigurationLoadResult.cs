namespace Core.Platform.Configuration;

/// <summary>Either the configuration in force, or every reason it was refused.</summary>
public sealed class ConfigurationLoadResult
{
    private ConfigurationLoadResult(
        EffectiveConfiguration? configuration,
        IReadOnlyList<ConfigurationProblem> problems)
    {
        Configuration = configuration;
        Problems = problems;
    }

    /// <summary>The configuration in force; null when loading refused it.</summary>
    public EffectiveConfiguration? Configuration { get; }

    /// <summary>Every reason for refusal; empty when the configuration loaded.</summary>
    public IReadOnlyList<ConfigurationProblem> Problems { get; }

    /// <summary>Whether loading refused the configuration.</summary>
    public bool Refused => Configuration is null;

    internal static ConfigurationLoadResult Loaded(EffectiveConfiguration configuration) =>
        new(configuration, []);

    internal static ConfigurationLoadResult Refusal(IReadOnlyList<ConfigurationProblem> problems) =>
        new(null, problems);
}
