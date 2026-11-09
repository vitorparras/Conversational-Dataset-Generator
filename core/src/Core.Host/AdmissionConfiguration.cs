using Core.Persistence.Ingestion;
using Core.Platform.Configuration;
using ConfigurationSection = Core.Platform.Configuration.ConfigurationSection;

namespace Core.Host;

/// <summary>The conditions under which material may come in, from the most restrictive to the most permissive: declared by every installation, with no default.</summary>
internal static class AdmissionConfiguration
{
    internal const string ConditionsPath = SectionName + ":" + ConditionsKey;

    internal const string Accepted = "nomes de letras minúsculas, algarismos e sublinhado, separados por vírgula, sem repetir, da condição mais restrita para a mais permissiva";

    private const string SectionName = "Ingestion";
    private const string ConditionsKey = "Conditions";

    internal static ConfigurationSection Section { get; } = new(SectionName, [ConfigurationKey.Required(ConditionsKey).Marking(Marked)]);

    /// <summary>The same section for the migration mode, which takes no material in: the list may be given, but is not required.</summary>
    internal static ConfigurationSection SectionWhenMigrating { get; } = new(SectionName, [ConfigurationKey.Optional(ConditionsKey).Marking(Marked)]);

    /// <summary>The refusal of a list declared that is empty in some place, repeats a name or holds what is not a name; none for one not declared, which the mode requires or not.</summary>
    internal static IReadOnlyList<ConfigurationProblem> Refusals(IReadOnlyDictionary<string, string> values) =>
        values.TryGetValue(ConditionsPath, out var declared) && OriginConditions.Refusal(Split(declared)) is not null
            ? [new ConfigurationProblem(ConfigurationProblemKind.ValueNotAccepted, ConditionsPath, declared, [Accepted])]
            : [];

    /// <summary>The conditions the configuration gave; the configuration has been judged before.</summary>
    internal static OriginConditions Conditions(IReadOnlyDictionary<string, string> values) => OriginConditions.Of(Split(values[ConditionsPath]));

    private static string[] Split(string declared) => [.. declared.Split(',').Select(name => name.Trim())];

    private static string Marked(string declared) => $"mais restrita: {Split(declared)[0]}";
}
