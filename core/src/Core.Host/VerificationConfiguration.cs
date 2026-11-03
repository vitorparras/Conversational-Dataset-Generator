using Core.Platform.Configuration;
using ConfigurationSection = Core.Platform.Configuration.ConfigurationSection;

namespace Core.Host;

/// <summary>
/// How often the chain of custody is verified, declared by every installation that serves and with no default,
/// since an interval chosen here would be the one every installation runs with.
/// </summary>
internal static class VerificationConfiguration
{
    internal const string VerificationIntervalPath = SectionName + ":" + VerificationIntervalKey;

    private const string SectionName = "Custody";
    private const string VerificationIntervalKey = "VerificationInterval";

    internal static ConfigurationSection Section { get; } = new(SectionName, [ConfigurationKey.Required(VerificationIntervalKey)]);

    /// <summary>The same section for the migration mode, which verifies nothing: the interval may be given, but is not required.</summary>
    internal static ConfigurationSection SectionWhenMigrating { get; } = new(SectionName, [ConfigurationKey.Optional(VerificationIntervalKey)]);

    /// <summary>The refusal of an interval declared that is not a duration above zero, in the format of the session limits; none for one not declared, which the mode requires or not.</summary>
    internal static IReadOnlyList<ConfigurationProblem> Refusals(IReadOnlyDictionary<string, string> values) =>
        values.TryGetValue(VerificationIntervalPath, out var declared) && SessionConfiguration.Duration(declared) is null
            ? [new ConfigurationProblem(ConfigurationProblemKind.ValueNotAccepted, VerificationIntervalPath, declared, [SessionConfiguration.Accepted])]
            : [];

    /// <summary>The interval the configuration gave; the configuration has been judged before.</summary>
    internal static VerificationSchedule Schedule(IReadOnlyDictionary<string, string> values) =>
        new(SessionConfiguration.Duration(values[VerificationIntervalPath])!.Value);
}
