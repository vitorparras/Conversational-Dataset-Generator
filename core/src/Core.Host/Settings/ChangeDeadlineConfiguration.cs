using Core.Platform.Configuration;
using ConfigurationSection = Core.Platform.Configuration.ConfigurationSection;

namespace Core.Host.Settings;

/// <summary>
/// How long a change of configuration submitted waits for a decision before it expires (decision 0053, option a): 72 h unless the installation declares another, from 1 h to 168 h.
/// The key is not one the screen changes: it changes only on the server.
/// </summary>
internal static class ChangeDeadlineConfiguration
{
    internal const string DeadlinePath = SectionName + ":" + DeadlineKey;

    private const string SectionName = "ConfigurationChange";
    private const string DeadlineKey = "PendingDeadline";
    private const string Accepted = "duração de 01:00:00 a 7.00:00:00 (de 1 a 168 horas), como 3.00:00:00";
    private const int ShortestHours = 1;
    private const int LongestHours = 168;

    internal static ConfigurationSection Section { get; } = new(SectionName, new[] { ConfigurationKey.WithDefault(DeadlineKey, "3.00:00:00") }.AsReadOnly());

    /// <summary>The refusal of a deadline declared outside the range; none for one within it or not declared.</summary>
    internal static IReadOnlyList<ConfigurationProblem> Refusals(IReadOnlyDictionary<string, string> values) =>
        values.TryGetValue(DeadlinePath, out var declared) && Deadline(declared) is null
            ? new[] { new ConfigurationProblem(ConfigurationProblemKind.ValueNotAccepted, DeadlinePath, declared, new[] { Accepted }.AsReadOnly()) }.AsReadOnly()
            : new List<ConfigurationProblem>().AsReadOnly();

    /// <summary>The deadline the configuration gave; the configuration has been judged before.</summary>
    internal static TimeSpan Of(IReadOnlyDictionary<string, string> values) => Deadline(values[DeadlinePath])!.Value;

    private static TimeSpan? Deadline(string declared) =>
        SessionConfiguration.Duration(declared) is { } deadline && deadline >= TimeSpan.FromHours(ShortestHours) && deadline <= TimeSpan.FromHours(LongestHours) ? deadline : null;
}
