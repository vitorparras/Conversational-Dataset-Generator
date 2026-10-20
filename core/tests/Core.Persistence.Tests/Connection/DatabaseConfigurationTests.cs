using Core.Persistence.Connection;
using Core.Platform.Configuration;
using Core.Platform.Configuration.Sources;
using Npgsql;

namespace Core.Persistence.Tests.Connection;

/// <summary>The application's credential comes from the closed configuration, all of it required, and its password is never shown.</summary>
public sealed class DatabaseConfigurationTests
{
    private const string Password = "ZQX-SENHA-7731";

    [Fact]
    public void ConnectionStringCarriesTheConfiguredServerDatabaseAndCredential()
    {
        var loaded = Load(Complete());

        var connection = new NpgsqlConnectionStringBuilder(DatabaseConfiguration.ConnectionString(loaded.Values));

        Assert.Equal("zqx-host:5433", connection.Host);
        Assert.Equal("zqx_database", connection.Database);
        Assert.Equal("zqx_app", connection.Username);
        Assert.Equal(Password, connection.Password);
    }

    [Fact]
    public void PasswordIsRedactedInTheConfigurationInForce()
    {
        var loaded = Load(Complete());

        var shown = Assert.Single(loaded.View.Entries, entry => entry.Key == "Database:Password");

        Assert.Equal(EffectiveConfiguration.Redacted, shown.Value);
        Assert.Equal(ConfigurationOrigin.Environment, shown.Origin);
    }

    [Fact]
    public void MissingPasswordRefusesTheConfigurationNamingTheKey()
    {
        var variables = Complete();
        _ = variables.Remove("CDG_Database__Password");

        var loaded = Load(variables);

        var problem = Assert.Single(loaded.Problems);
        Assert.Equal(ConfigurationProblemKind.MissingKey, problem.Kind);
        Assert.Equal("Database:Password", problem.Key);
    }

    private static ConfigurationLoadResult Load(Dictionary<string, string?> variables) =>
        ClosedConfiguration.Load(new ConfigurationSchema([DatabaseConfiguration.Section]), new EnvironmentSource(variables));

    private static Dictionary<string, string?> Complete() =>
        new(StringComparer.Ordinal)
        {
            ["CDG_Database__Host"] = "zqx-host:5433",
            ["CDG_Database__Name"] = "zqx_database",
            ["CDG_Database__Username"] = "zqx_app",
            ["CDG_Database__Password"] = Password,
        };
}
