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

    private static readonly string[] Paths = [HostPath, NamePath, UsernamePath, PasswordPath];

    /// <summary>
    /// The section the composition root adds to the schema: the server (a name or address, optionally followed by
    /// <c>:port</c>), the database, and the application's role and password, the password never shown. The section is
    /// optional while nothing persists; once any of the four is declared, all four are required.
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
    /// The settings the approved values make: not configured when no key is declared; refused, naming every key at fault, when
    /// only part of the section is, or when the host is not one server with an optional port of 1 to 65535; otherwise the
    /// connection string.
    /// </summary>
    public static DatabaseSettings Read(IReadOnlyDictionary<string, string> values) => Read(values, required: false);

    /// <summary>
    /// The settings for the mode that migrates the schema, which has nothing to run against without a database: as
    /// <see cref="Read(IReadOnlyDictionary{string, string})"/>, but a section with no key declared is refused, naming all four.
    /// </summary>
    public static DatabaseSettings ReadRequired(IReadOnlyDictionary<string, string> values) => Read(values, required: true);

    private static DatabaseSettings Read(IReadOnlyDictionary<string, string> values, bool required)
    {
        ArgumentNullException.ThrowIfNull(values);
        var missing = Paths.Where(path => !values.ContainsKey(path)).ToList();
        if (missing.Count == Paths.Length && !required)
        {
            return DatabaseSettings.NotConfigured;
        }

        ConfigurationProblem[] problems =
        [
            .. missing.Select(path => new ConfigurationProblem(ConfigurationProblemKind.MissingKey, path, Found: null, Accepted: null)),
            .. values.TryGetValue(HostPath, out var host) && !IsOneServer(host) ? [HostRefused(host)] : Array.Empty<ConfigurationProblem>(),
        ];
        return problems.Length > 0 ? DatabaseSettings.Refused(problems) : DatabaseSettings.Configured(ConnectionString(host!, values));
    }

    /// <summary>A name or address, never a list nor an IPv6 literal, optionally followed by a port written in digits only.</summary>
    private static bool IsOneServer(string host)
    {
        var parts = host.Split(':');
        var name = !host.Contains(',', StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(parts[0]);
        return name && (parts.Length == 1 || (parts.Length == 2 && IsPort(parts[1])));
    }

    private static bool IsPort(string port) =>
        int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number is > 0 and <= HighestPort;

    private static ConfigurationProblem HostRefused(string host) =>
        new(ConfigurationProblemKind.ValueNotAccepted, HostPath, host, ["um servidor, ou servidor:porta com porta de 1 a 65535; sem lista nem IPv6 literal"]);

    private static string ConnectionString(string host, IReadOnlyDictionary<string, string> values) =>
        new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Database = values[NamePath],
            Username = values[UsernamePath],
            Password = values[PasswordPath],
        }.ConnectionString;
}
