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

    private const string SectionName = "Audit";
    private const string ServerLogDirectoryKey = "ServerLogDirectory";
    private const string Accepted = "caminho absoluto da pasta onde o servidor de banco escreve o registro csv, como /var/log/postgresql";

    internal static ConfigurationSection Section { get; } = new(SectionName, [ConfigurationKey.Required(ServerLogDirectoryKey)]);

    /// <summary>The same section for the migration mode, which reads no log: the folder may be given, and is then judged like any other start, but is not required.</summary>
    internal static ConfigurationSection SectionWhenMigrating { get; } = new(SectionName, [ConfigurationKey.Optional(ServerLogDirectoryKey)]);

    /// <summary>The refusal of a folder declared that is not an absolute path; none for one not declared, which the mode requires or not.</summary>
    internal static IReadOnlyList<ConfigurationProblem> Refusals(IReadOnlyDictionary<string, string> values) =>
        values.TryGetValue(ServerLogDirectoryPath, out var declared) && !Path.IsPathRooted(declared)
            ? [new ConfigurationProblem(ConfigurationProblemKind.ValueNotAccepted, ServerLogDirectoryPath, declared, [Accepted])]
            : [];

    /// <summary>The folder the configuration gave; the configuration has been judged before.</summary>
    internal static string ServerLogDirectory(IReadOnlyDictionary<string, string> values) => values[ServerLogDirectoryPath];
}
