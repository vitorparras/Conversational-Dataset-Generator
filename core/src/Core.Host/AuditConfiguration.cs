using System.Globalization;
using Core.Platform.Configuration;
using ConfigurationSection = Core.Platform.Configuration.ConfigurationSection;

namespace Core.Host;

/// <summary>
/// The folder where the database server writes its audit log, declared by every installation that serves and with no default,
/// since a folder chosen here would be one most installations do not have.
/// </summary>
internal static class AuditConfiguration
{
    internal const string ServerLogDirectoryPath = SectionName + ":" + ServerLogDirectoryKey;

    internal const string FromPath = SectionName + ":" + FromKey;

    internal const string ToPath = SectionName + ":" + ToKey;

    private const string SectionName = "Audit";
    private const string FromKey = "From";
    private const string ToKey = "To";
    private const string AcceptedInstant = "instante com fuso, como 2026-11-01T12:00:00Z, e From vem antes de To";
    private const string ServerLogDirectoryKey = "ServerLogDirectory";
    private const string Accepted = "caminho absoluto da pasta onde o servidor de banco escreve o registro csv, como /var/log/postgresql";

    internal static ConfigurationSection Section { get; } = new(SectionName, [ConfigurationKey.Required(ServerLogDirectoryKey)]);

    /// <summary>The same section for the migration mode, which reads no log: the folder may be given, and is then judged like any other start, but is not required.</summary>
    internal static ConfigurationSection SectionWhenMigrating { get; } = new(SectionName, [ConfigurationKey.Optional(ServerLogDirectoryKey)]);

    /// <summary>The section of the comparison mode, which serves nothing and compares an interval: the folder and both ends of the interval are required.</summary>
    internal static ConfigurationSection SectionWhenComparing { get; } =
        new(SectionName, [ConfigurationKey.Required(ServerLogDirectoryKey), ConfigurationKey.Required(FromKey), ConfigurationKey.Required(ToKey)]);

    /// <summary>The refusal of a folder declared that is not an absolute path; none for one not declared, which the mode requires or not.</summary>
    internal static IReadOnlyList<ConfigurationProblem> Refusals(IReadOnlyDictionary<string, string> values) =>
        [.. FolderRefusals(values), .. IntervalRefusals(values)];

    /// <summary>An instant with a zone, in UTC; none for text that has no zone, because a time read in the zone of whoever runs it names another instant elsewhere.</summary>
    internal static DateTimeOffset? Instant(string? text) =>
        DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var instant) && instant.Kind != DateTimeKind.Unspecified
            ? new DateTimeOffset(instant.ToUniversalTime())
            : null;

    /// <summary>The interval the comparison mode was given; the configuration has been judged before.</summary>
    internal static (DateTimeOffset From, DateTimeOffset To) Interval(IReadOnlyDictionary<string, string> values) =>
        (Instant(values[FromPath])!.Value, Instant(values[ToPath])!.Value);

    /// <summary>The folder the configuration gave; the configuration has been judged before.</summary>
    internal static string ServerLogDirectory(IReadOnlyDictionary<string, string> values) => values[ServerLogDirectoryPath];

    private static IEnumerable<ConfigurationProblem> FolderRefusals(IReadOnlyDictionary<string, string> values) =>
        values.TryGetValue(ServerLogDirectoryPath, out var declared) && !Path.IsPathRooted(declared)
            ? [new ConfigurationProblem(ConfigurationProblemKind.ValueNotAccepted, ServerLogDirectoryPath, declared, [Accepted])]
            : [];

    /// <summary>Each end declared that is not an instant with a zone, or the two in the wrong order; none when the mode gave neither.</summary>
    private static IEnumerable<ConfigurationProblem> IntervalRefusals(IReadOnlyDictionary<string, string> values)
    {
        var ends = new[] { FromPath, ToPath }.Where(values.ContainsKey).ToArray();
        var notInstants = ends.Where(path => Instant(values[path]) is null);
        if (notInstants.Any() || ends.Length < 2)
        {
            return notInstants.Select(path => Refused(path, values[path]));
        }

        return Instant(values[FromPath]) < Instant(values[ToPath]) ? [] : [Refused(ToPath, values[ToPath])];
    }

    private static ConfigurationProblem Refused(string path, string declared) =>
        new(ConfigurationProblemKind.ValueNotAccepted, path, declared, [AcceptedInstant]);
}
