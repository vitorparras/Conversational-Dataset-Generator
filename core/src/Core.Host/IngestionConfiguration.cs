using System.Globalization;
using Core.Persistence.Ingestion;
using Core.Platform.Configuration;
using ConfigurationSection = Core.Platform.Configuration.ConfigurationSection;

namespace Core.Host;

/// <summary>
/// The conditions under which material may come in, from the most restrictive to the most permissive: declared by every installation, with no default;
/// and the most bytes a file may have, which has a default of ten mebibytes.
/// </summary>
internal static class IngestionConfiguration
{
    internal const string ConditionsPath = SectionName + ":" + ConditionsKey;

    internal const string Accepted = "nomes de letras minúsculas, algarismos e sublinhado, separados por vírgula, sem repetir, da condição mais restrita para a mais permissiva";

    internal const string MaxBytesPath = SectionName + ":" + MaxBytesKey;

    internal const string MaxBytesAccepted = "um número inteiro de bytes, de 1 até 1073741824 (1 GiB)";

    internal const long MostBytes = 1_073_741_824;

    private const string SectionName = "Ingestion";
    private const string ConditionsKey = "Conditions";
    private const string MaxBytesKey = "MaxBytes";
    private const string DefaultMaxBytes = "10485760";

    internal static ConfigurationSection Section { get; } =
        new(SectionName, [ConfigurationKey.Required(ConditionsKey).Marking(Marked), ConfigurationKey.WithDefault(MaxBytesKey, DefaultMaxBytes)]);

    /// <summary>The same section for the migration mode, which takes no material in: the list may be given, but is not required.</summary>
    internal static ConfigurationSection SectionWhenMigrating { get; } =
        new(SectionName, [ConfigurationKey.Optional(ConditionsKey).Marking(Marked), ConfigurationKey.WithDefault(MaxBytesKey, DefaultMaxBytes)]);

    /// <summary>The refusal of a list declared that is empty in some place, repeats a name or holds what is not a name, and of a size that is not a whole number of bytes in range.</summary>
    internal static IReadOnlyList<ConfigurationProblem> Refusals(IReadOnlyDictionary<string, string> values)
    {
        var problems = new List<ConfigurationProblem>();
        if (values.TryGetValue(ConditionsPath, out var declared) && OriginConditions.Refusal(Split(declared)) is not null)
        {
            problems.Add(new ConfigurationProblem(ConfigurationProblemKind.ValueNotAccepted, ConditionsPath, declared, [Accepted]));
        }

        if (values.TryGetValue(MaxBytesPath, out var size) && Bytes(size) is null)
        {
            problems.Add(new ConfigurationProblem(ConfigurationProblemKind.ValueNotAccepted, MaxBytesPath, size, [MaxBytesAccepted]));
        }

        return problems;
    }

    /// <summary>The conditions the configuration gave; the configuration has been judged before.</summary>
    internal static OriginConditions Conditions(IReadOnlyDictionary<string, string> values) => OriginConditions.Of(Split(values[ConditionsPath]));

    /// <summary>The most bytes a file may have, the configuration's or the default; the configuration has been judged before.</summary>
    internal static long MaxBytes(IReadOnlyDictionary<string, string> values) => Bytes(values[MaxBytesPath])!.Value;

    private static long? Bytes(string declared) =>
        long.TryParse(declared, NumberStyles.None, CultureInfo.InvariantCulture, out var bytes) && bytes is > 0 and <= MostBytes ? bytes : null;

    private static string[] Split(string declared) => [.. declared.Split(',').Select(name => name.Trim())];

    private static string Marked(string declared) => $"mais restrita: {Split(declared)[0]}";
}
