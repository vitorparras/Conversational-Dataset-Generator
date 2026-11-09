using System.Globalization;
using Core.Persistence.Ingestion;
using Core.Platform.Configuration;
using ConfigurationSection = Core.Platform.Configuration.ConfigurationSection;

namespace Core.Host;

/// <summary>The conditions under which material may come in, from the most restrictive to the most permissive: declared by every installation, with no default.</summary>
internal static class AdmissionConfiguration
{
    internal const string ConditionsPath = SectionName + ":" + ConditionsKey;

    internal const string Accepted = "nomes de letras minúsculas, algarismos e sublinhado, separados por vírgula, sem repetir, da condição mais restrita para a mais permissiva";

    internal const string SampleSizePath = SectionName + ":" + SampleSizeKey;

    internal const string AcceptedSampleSize = "um número inteiro de 1 em diante";

    private const string SectionName = "Ingestion";
    private const string ConditionsKey = "Conditions";
    private const string SampleSizeKey = "ReviewSampleSize";

    internal static ConfigurationSection Section { get; } = new(SectionName, [ConfigurationKey.Required(ConditionsKey).Marking(Marked), ConfigurationKey.Optional(SampleSizeKey)]);

    /// <summary>The same section for the migration mode, which takes no material in: the list may be given, but is not required.</summary>
    internal static ConfigurationSection SectionWhenMigrating { get; } = new(SectionName, [ConfigurationKey.Optional(ConditionsKey).Marking(Marked), ConfigurationKey.Optional(SampleSizeKey)]);

    /// <summary>The refusal of a list declared that is empty in some place, repeats a name or holds what is not a name; none for one not declared, which the mode requires or not.</summary>
    internal static IReadOnlyList<ConfigurationProblem> Refusals(IReadOnlyDictionary<string, string> values)
    {
        var refused = ConditionRefusals(values).Concat(SampleSizeRefusals(values)).ToList();
        return refused;
    }

    /// <summary>How many items a sample of the review holds at most, as the installation declares it; null when it does not declare one.</summary>
    /// <param name="values">The values in force.</param>
    internal static int? ReviewSampleSize(IReadOnlyDictionary<string, string> values) =>
        values.TryGetValue(SampleSizePath, out var declared) && SampleSize(declared) is { } size ? size : null;

    private static IReadOnlyList<ConfigurationProblem> ConditionRefusals(IReadOnlyDictionary<string, string> values) =>
        values.TryGetValue(ConditionsPath, out var declared) && OriginConditions.Refusal(Split(declared)) is not null
            ? [new ConfigurationProblem(ConfigurationProblemKind.ValueNotAccepted, ConditionsPath, declared, [Accepted])]
            : [];

    /// <summary>The conditions the configuration gave; the configuration has been judged before.</summary>
    internal static OriginConditions Conditions(IReadOnlyDictionary<string, string> values) => OriginConditions.Of(Split(values[ConditionsPath]));

    private static IEnumerable<ConfigurationProblem> SampleSizeRefusals(IReadOnlyDictionary<string, string> values)
    {
        if (values.TryGetValue(SampleSizePath, out var declared) && SampleSize(declared) is null)
        {
            var accepted = Enumerable.Repeat(AcceptedSampleSize, 1).ToList();
            yield return new ConfigurationProblem(ConfigurationProblemKind.ValueNotAccepted, SampleSizePath, declared, accepted);
        }
    }

    private static int? SampleSize(string declared) =>
        int.TryParse(declared, NumberStyles.None, CultureInfo.InvariantCulture, out var size) && size >= 1 ? size : null;

    private static string[] Split(string declared) => [.. declared.Split(',').Select(name => name.Trim())];

    private static string Marked(string declared) => $"mais restrita: {Split(declared)[0]}";
}
