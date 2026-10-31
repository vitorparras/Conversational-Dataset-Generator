using System.Globalization;
using Core.Persistence.People;
using Core.Platform.Configuration;
using ConfigurationSection = Core.Platform.Configuration.ConfigurationSection;

namespace Core.Host;

/// <summary>The work factor of the derivation of passwords: declared by every installation, with no default, since a number chosen here would be the one every installation runs with.</summary>
internal static class PasswordConfiguration
{
    internal const string WorkFactorPath = SectionName + ":" + WorkFactorKey;

    private const string SectionName = "Passwords";
    private const string WorkFactorKey = "WorkFactor";
    private const string Accepted = "número inteiro positivo de iterações da derivação";

    internal static ConfigurationSection Section { get; } = new(SectionName, [ConfigurationKey.Required(WorkFactorKey)]);

    /// <summary>The same section for the migration mode, which derives no password: the factor may be given, and is then judged like any other start, but is not required.</summary>
    internal static ConfigurationSection SectionWhenMigrating { get; } = new(SectionName, [ConfigurationKey.Optional(WorkFactorKey)]);

    /// <summary>The refusal of a work factor declared that is not a whole number above zero; none when it is one, or when it is not declared and the mode does not require it.</summary>
    internal static IReadOnlyList<ConfigurationProblem> Refusals(IReadOnlyDictionary<string, string> values) =>
        values.TryGetValue(WorkFactorPath, out var declared) && WorkFactor(declared) is null
            ? [new ConfigurationProblem(ConfigurationProblemKind.ValueNotAccepted, WorkFactorPath, declared, [Accepted])]
            : [];

    /// <summary>The derivation that runs with the work factor the configuration gave; the configuration has been judged before.</summary>
    internal static PasswordDerivation Derivation(IReadOnlyDictionary<string, string> values) => new(WorkFactor(values[WorkFactorPath])!.Value);

    private static int? WorkFactor(string declared) =>
        int.TryParse(declared, NumberStyles.None, CultureInfo.InvariantCulture, out var factor) && factor > 0 ? factor : null;
}
