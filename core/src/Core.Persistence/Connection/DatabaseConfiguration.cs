using System.Globalization;
using Core.Platform.Configuration;
using Npgsql;

namespace Core.Persistence.Connection;

/// <summary>The keys that reach the database with the application's credential, and what the composition root makes of them.</summary>
public static class DatabaseConfiguration
{
    internal const string HostPath = SectionName + ":" + HostKey;

    internal const string UsernamePath = SectionName + ":" + UsernameKey;

    internal const string NamePath = SectionName + ":" + NameKey;

    private const string PasswordPath = SectionName + ":" + PasswordKey;

    private const string SectionName = "Database";
    private const string HostKey = "Host";
    private const string NameKey = "Name";
    private const string UsernameKey = "Username";
    private const string PasswordKey = "Password";
    private const int HighestPort = 65535;

    private static readonly string[] Companions = [NamePath, UsernamePath, PasswordPath];

    /// <summary>
    /// The section the composition root adds to the schema: the server (a name or address, optionally followed by
    /// <c>:port</c>), the database, and the application's role and password, the password never shown. The section is
    /// optional while nothing persists; once <c>Database:Host</c> is declared, the other three are required.
    /// </summary>
    public static ConfigurationSection Section { get; } =
        new(
            SectionName,
            [
                ConfigurationKey.Optional(HostKey),
                ConfigurationKey.Optional(NameKey),
                ConfigurationKey.Optional(UsernameKey),
                ConfigurationKey.Optional(PasswordKey).AsSensitive(),
            ]);

    /// <summary>
    /// The settings the approved values make: not configured without <c>Database:Host</c>; refused, naming every key at
    /// fault, when the host is declared without the rest or with a port that is not a number; otherwise the connection string.
    /// </summary>
    public static DatabaseSettings Read(IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (!values.TryGetValue(HostPath, out var host))
        {
            return DatabaseSettings.NotConfigured;
        }

        ConfigurationProblem[] problems =
        [
            .. Companions.Where(path => !values.ContainsKey(path))
                .Select(path => new ConfigurationProblem(ConfigurationProblemKind.MissingKey, path, Found: null, Accepted: null)),
            .. PortProblems(host),
        ];
        return problems.Length > 0 ? DatabaseSettings.Refused(problems) : DatabaseSettings.Configured(ConnectionString(host, values));
    }

    private static IEnumerable<ConfigurationProblem> PortProblems(string host)
    {
        var separator = host.LastIndexOf(':');
        var port = separator < 0 ? null : host[(separator + 1)..];
        return port is null || (int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number is > 0 and <= HighestPort)
            ? []
            : [new ConfigurationProblem(ConfigurationProblemKind.ValueNotAccepted, HostPath, host, ["servidor, ou servidor:porta com porta de 1 a 65535"])];
    }

    private static string ConnectionString(string host, IReadOnlyDictionary<string, string> values) =>
        new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Database = values[NamePath],
            Username = values[UsernamePath],
            Password = values[PasswordPath],
        }.ConnectionString;
}
