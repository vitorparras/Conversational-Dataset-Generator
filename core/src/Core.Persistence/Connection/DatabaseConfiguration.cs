using Core.Platform.Configuration;
using Npgsql;

namespace Core.Persistence.Connection;

/// <summary>The keys that reach the database with the application's credential, and the connection string they make.</summary>
public static class DatabaseConfiguration
{
    internal const string HostPath = SectionName + ":" + HostKey;

    internal const string UsernamePath = SectionName + ":" + UsernameKey;

    private const string SectionName = "Database";
    private const string HostKey = "Host";
    private const string NameKey = "Name";
    private const string UsernameKey = "Username";
    private const string PasswordKey = "Password";

    /// <summary>
    /// The section the composition root adds to the schema: the server (a name or address, optionally followed by
    /// <c>:port</c>), the database, and the application's role and password, every one required and the password never shown.
    /// </summary>
    public static ConfigurationSection Section { get; } =
        new(
            SectionName,
            [
                ConfigurationKey.Required(HostKey),
                ConfigurationKey.Required(NameKey),
                ConfigurationKey.Required(UsernameKey),
                ConfigurationKey.Required(PasswordKey).AsSensitive(),
            ]);

    /// <summary>The connection string of the application's role, from the values the closed configuration approved.</summary>
    public static string ConnectionString(IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new NpgsqlConnectionStringBuilder
        {
            Host = values[HostPath],
            Database = values[SectionName + ":" + NameKey],
            Username = values[UsernamePath],
            Password = values[SectionName + ":" + PasswordKey],
        }.ConnectionString;
    }
}
