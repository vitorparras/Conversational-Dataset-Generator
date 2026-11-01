using System.Globalization;
using Core.Platform.Configuration;
using Core.Platform.Sessions;
using ConfigurationSection = Core.Platform.Configuration.ConfigurationSection;

namespace Core.Host;

/// <summary>The two limits of a session, declared by every installation and with no default, since a number chosen here would be the one every installation runs with.</summary>
internal static class SessionConfiguration
{
    internal const string IdleTimeoutPath = SectionName + ":" + IdleTimeoutKey;

    internal const string AbsoluteTimeoutPath = SectionName + ":" + AbsoluteTimeoutKey;

    private const string SectionName = "Session";
    private const string IdleTimeoutKey = "IdleTimeout";
    private const string AbsoluteTimeoutKey = "AbsoluteTimeout";
    private const string Accepted = "duração maior que zero, como 00:30:00 (horas:minutos:segundos) ou 1.00:00:00 (dias.horas:minutos:segundos)";

    private static readonly string[] Formats = [@"h\:mm\:ss", @"d\.h\:mm\:ss"];

    internal static ConfigurationSection Section { get; } = new(SectionName, [ConfigurationKey.Required(IdleTimeoutKey), ConfigurationKey.Required(AbsoluteTimeoutKey)]);

    /// <summary>The same section for the migration mode, which opens no session: the limits may be given, but are not required.</summary>
    internal static ConfigurationSection SectionWhenMigrating { get; } = new(SectionName, [ConfigurationKey.Optional(IdleTimeoutKey), ConfigurationKey.Optional(AbsoluteTimeoutKey)]);

    /// <summary>The refusal of each limit declared that is not a duration above zero; none for one not declared, which the mode requires or not.</summary>
    internal static IReadOnlyList<ConfigurationProblem> Refusals(IReadOnlyDictionary<string, string> values) =>
        [.. new[] { IdleTimeoutPath, AbsoluteTimeoutPath }
            .Where(path => values.TryGetValue(path, out var declared) && Duration(declared) is null)
            .Select(path => new ConfigurationProblem(ConfigurationProblemKind.ValueNotAccepted, path, values[path], [Accepted]))];

    /// <summary>The limits the configuration gave; the configuration has been judged before.</summary>
    internal static SessionLifetimes Lifetimes(IReadOnlyDictionary<string, string> values) =>
        new(Duration(values[IdleTimeoutPath])!.Value, Duration(values[AbsoluteTimeoutPath])!.Value);

    private static TimeSpan? Duration(string declared) =>
        TimeSpan.TryParseExact(declared, Formats, CultureInfo.InvariantCulture, TimeSpanStyles.None, out var duration) && duration > TimeSpan.Zero ? duration : null;
}
