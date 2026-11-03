using System.Globalization;
using Core.Platform.Configuration;
using ConfigurationSection = Core.Platform.Configuration.ConfigurationSection;

namespace Core.Host;

/// <summary>
/// The folder where the database server writes its audit log and how often the host compares it with the chain, declared by every installation
/// that serves and with no default, since a folder or a period chosen here would be one most installations do not have.
/// </summary>
internal static class AuditConfiguration
{
    internal const string ServerLogDirectoryPath = SectionName + ":" + ServerLogDirectoryKey;
    internal const string FromPath = SectionName + ":" + FromKey;
    internal const string ToPath = SectionName + ":" + ToKey;
    internal const string CompareIntervalPath = SectionName + ":" + CompareIntervalKey;
    private const string SectionName = "Audit";
    private const string FromKey = "From";
    private const string ToKey = "To";
    private const string CompareIntervalKey = "CompareInterval";
    private const string AcceptedPeriod = "duração de 00:01:00 a 49.00:00:00, como 01:00:00 (horas:minutos:segundos) ou 1.00:00:00 (dias.horas:minutos:segundos)";
    private const string AcceptedInstant = "instante com fuso, como 2026-11-01T12:00:00Z, e From vem antes de To";
    private const string ServerLogDirectoryKey = "ServerLogDirectory";
    private const string Accepted = "caminho absoluto da pasta onde o servidor de banco escreve o registro csv, como /var/log/postgresql";

    /// <summary>The timer that waits for the period takes its milliseconds in an unsigned integer, so it refuses one past 49.7 days; this is the longest whole day under that.</summary>
    private static readonly TimeSpan LongestPeriod = TimeSpan.FromDays(49);

    internal static ConfigurationSection Section { get; } =
        new(SectionName, Listed(ConfigurationKey.Required(ServerLogDirectoryKey), ConfigurationKey.Required(CompareIntervalKey)));

    /// <summary>The same section for the migration mode, which reads no log: the folder may be given, and is then judged like any other start, but is not required.</summary>
    internal static ConfigurationSection SectionWhenMigrating { get; } = new(SectionName, Listed(ConfigurationKey.Optional(ServerLogDirectoryKey), ConfigurationKey.Optional(CompareIntervalKey)));

    /// <summary>The section of the comparison mode, which serves nothing and compares an interval: the folder and both ends of the interval are required.</summary>
    internal static ConfigurationSection SectionWhenComparing { get; } =
        new(SectionName, Listed(ConfigurationKey.Required(ServerLogDirectoryKey), ConfigurationKey.Required(FromKey), ConfigurationKey.Required(ToKey), ConfigurationKey.Optional(CompareIntervalKey)));
    /// <summary>The refusals of what was declared: the folder, the ends of the interval and the period; none for what was not declared.</summary>
    internal static IReadOnlyList<ConfigurationProblem> Refusals(IReadOnlyDictionary<string, string> values)
    {
        var found = new[] { FolderRefusals(values), IntervalRefusals(values), PeriodRefusals(values) }.SelectMany(refusals => refusals).ToArray();
        return found;
    }

    /// <summary>An instant with a zone, in UTC; none for text that has no zone, because a time read in the zone of whoever runs it names another instant elsewhere.</summary>
    internal static DateTimeOffset? Instant(string? text) =>
        DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var instant) && instant.Kind != DateTimeKind.Unspecified
            ? new DateTimeOffset(instant.ToUniversalTime())
            : null;

    /// <summary>The interval the comparison mode was given; the configuration has been judged before.</summary>
    internal static (DateTimeOffset From, DateTimeOffset To) Interval(IReadOnlyDictionary<string, string> values) =>
        (Instant(values[FromPath])!.Value, Instant(values[ToPath])!.Value);

    /// <summary>How often the periodic comparison runs; the configuration has been judged before.</summary>
    internal static TimeSpan CompareInterval(IReadOnlyDictionary<string, string> values) => Period(values[CompareIntervalPath])!.Value;

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

    private static IEnumerable<ConfigurationProblem> PeriodRefusals(IReadOnlyDictionary<string, string> values)
    {
        if (values.TryGetValue(CompareIntervalPath, out var declared) && Period(declared) is null)
        {
            yield return new ConfigurationProblem(ConfigurationProblemKind.ValueNotAccepted, CompareIntervalPath, declared, Listed(AcceptedPeriod));
        }
    }

    private static TimeSpan? Period(string declared) =>
        Parsed(declared) is { } period && period >= TimeSpan.FromMinutes(1) && period <= LongestPeriod ? period : null;

    private static TimeSpan? Parsed(string declared) =>
        TimeSpan.TryParseExact(declared, @"h\:mm\:ss", CultureInfo.InvariantCulture, out var period)
            || TimeSpan.TryParseExact(declared, @"d\.h\:mm\:ss", CultureInfo.InvariantCulture, out period)
            ? period
            : null;

    /// <summary>The items as a list, written without a collection expression.</summary>
    private static T[] Listed<T>(params T[] items) => items;

    private static ConfigurationProblem Refused(string path, string declared) =>
        new(ConfigurationProblemKind.ValueNotAccepted, path, declared, [AcceptedInstant]);
}
