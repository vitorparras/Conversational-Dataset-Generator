using System.Globalization;
using Core.Platform;
using Core.Platform.Configuration;
using ConfigurationSection = Core.Platform.Configuration.ConfigurationSection;

namespace Core.Host;

/// <summary>
/// What a real installation declares about its safekeeping copy: when the recovery was last tested and who holds the copy.
/// The system cannot check either, so it asks for the declaration and writes it down.
/// </summary>
internal static class SafekeepingConfiguration
{
    internal const string LastRecoveryTestPath = SectionName + ":" + LastRecoveryTestKey;

    internal const string HolderPath = SectionName + ":" + HolderKey;

    private const string SectionName = "Custody";
    private const string LastRecoveryTestKey = "LastRecoveryTest";
    private const string HolderKey = "Holder";
    private const string DateFormat = "yyyy-MM-dd";

    /// <summary>Both keys are optional to the schema, which does not know the kind; a demonstration needs neither, and a real installation is held to them by <see cref="Refusals"/>.</summary>
    internal static ConfigurationSection Section { get; } =
        new(SectionName, [ConfigurationKey.Optional(LastRecoveryTestKey), ConfigurationKey.Optional(HolderKey).AsSensitive()]);

    /// <summary>What a real installation lacks in its declaration, naming every key at fault; a demonstration lacks nothing.</summary>
    internal static IReadOnlyList<ConfigurationProblem> Refusals(IReadOnlyDictionary<string, string> values, InstallationKind kind)
    {
        if (kind != InstallationKind.Real)
        {
            return [];
        }

        return
        [
            .. new[] { LastRecoveryTestPath, HolderPath }
                .Where(path => !values.ContainsKey(path))
                .Select(path => new ConfigurationProblem(ConfigurationProblemKind.MissingKey, path, Found: null, Accepted: null)),
            .. values.TryGetValue(LastRecoveryTestPath, out var date) && !IsIsoDate(date)
                ? [new ConfigurationProblem(ConfigurationProblemKind.ValueNotAccepted, LastRecoveryTestPath, date, [$"uma data ISO, {DateFormat}"])]
                : Array.Empty<ConfigurationProblem>(),
        ];
    }

    /// <summary>What the installation declared, once both keys are; null when it did not declare them.</summary>
    internal static SafekeepingDeclaration? Declaration(IReadOnlyDictionary<string, string> values) =>
        values.TryGetValue(LastRecoveryTestPath, out var date) && values.TryGetValue(HolderPath, out var holder)
            ? new SafekeepingDeclaration(date, holder)
            : null;

    private static bool IsIsoDate(string text) =>
        DateOnly.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}
