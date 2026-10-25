using Core.Platform.Configuration;

namespace Core.Persistence.Connection;

/// <summary>What the configuration says about the database: not configured, refused with its problems, or the connection string.</summary>
public sealed class DatabaseSettings
{
    private DatabaseSettings(DatabaseSettingsState state, string? connectionString, IReadOnlyList<ConfigurationProblem> problems)
    {
        State = state;
        ConnectionString = connectionString;
        Problems = problems;
    }

    /// <summary>Which of the three the configuration says.</summary>
    public DatabaseSettingsState State { get; }

    /// <summary>The application's connection string when configured; null otherwise. It holds the password: never show it.</summary>
    public string? ConnectionString { get; }

    /// <summary>Every key at fault when refused; empty otherwise.</summary>
    public IReadOnlyList<ConfigurationProblem> Problems { get; }

    internal static DatabaseSettings NotConfigured { get; } = new(DatabaseSettingsState.NotConfigured, connectionString: null, []);

    internal static DatabaseSettings Refused(IReadOnlyList<ConfigurationProblem> problems) =>
        new(DatabaseSettingsState.Refused, connectionString: null, problems);

    internal static DatabaseSettings Configured(string connectionString) =>
        new(DatabaseSettingsState.Configured, connectionString, []);
}
