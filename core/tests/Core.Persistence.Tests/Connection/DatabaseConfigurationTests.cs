using Core.Persistence.Connection;
using Core.Platform.Configuration;
using Core.Platform.Configuration.Sources;
using Npgsql;

namespace Core.Persistence.Tests.Connection;

/// <summary>
/// The application's credential comes from the closed configuration: without a host the database is not configured; with a
/// host, the rest is required and the port must be a number; the password is never shown.
/// </summary>
public sealed class DatabaseConfigurationTests
{
    private const string Password = "ZQX-SENHA-7731";

    [Fact]
    public void ConnectionStringCarriesTheConfiguredServerDatabaseAndCredential()
    {
        var settings = DatabaseConfiguration.Read(Load(Complete()).Values);

        var connection = new NpgsqlConnectionStringBuilder(settings.ConnectionString);

        Assert.Equal(DatabaseSettingsState.Configured, settings.State);
        Assert.Equal("zqx-host:5433", connection.Host);
        Assert.Equal("zqx_database", connection.Database);
        Assert.Equal("zqx_app", connection.Username);
        Assert.Equal(Password, connection.Password);
    }

    [Fact]
    public void HostWithoutPortIsAccepted()
    {
        var variables = Complete();
        variables["CDG_Database__Host"] = "zqx-host";

        var settings = DatabaseConfiguration.Read(Load(variables).Values);

        Assert.Equal(DatabaseSettingsState.Configured, settings.State);
    }

    [Fact]
    public void NoSectionLoadsAndLeavesTheDatabaseNotConfigured()
    {
        var loaded = Load([]);

        var settings = DatabaseConfiguration.Read(loaded.Values);

        Assert.False(loaded.Refused);
        Assert.Equal(DatabaseSettingsState.NotConfigured, settings.State);
        Assert.Null(settings.ConnectionString);
    }

    [Fact]
    public void HostWithoutThePasswordIsRefusedNamingTheKey()
    {
        var variables = Complete();
        _ = variables.Remove("CDG_Database__Password");

        var settings = DatabaseConfiguration.Read(Load(variables).Values);

        Assert.Equal(DatabaseSettingsState.Refused, settings.State);
        var problem = Assert.Single(settings.Problems);
        Assert.Equal(ConfigurationProblemKind.MissingKey, problem.Kind);
        Assert.Equal("Database:Password", problem.Key);
        Assert.Null(settings.ConnectionString);
    }

    [Theory]
    [InlineData("zqx-host:zqx")]
    [InlineData("zqx-host:0")]
    [InlineData("zqx-host:65536")]
    [InlineData("zqx-host:")]
    public void PortThatIsNotAServerPortIsRefusedNamingTheHostKey(string host)
    {
        var variables = Complete();
        variables["CDG_Database__Host"] = host;

        var settings = DatabaseConfiguration.Read(Load(variables).Values);

        var problem = Assert.Single(settings.Problems);
        Assert.Equal(ConfigurationProblemKind.ValueNotAccepted, problem.Kind);
        Assert.Equal("Database:Host", problem.Key);
        Assert.Equal(host, problem.Found);
    }

    [Fact]
    public void HighestPortIsAccepted()
    {
        var variables = Complete();
        variables["CDG_Database__Host"] = "zqx-host:65535";

        var settings = DatabaseConfiguration.Read(Load(variables).Values);

        Assert.Equal(DatabaseSettingsState.Configured, settings.State);
    }

    [Fact]
    public void PasswordIsRedactedInTheConfigurationInForce()
    {
        var loaded = Load(Complete());

        var shown = Assert.Single(loaded.View.Entries, entry => entry.Key == "Database:Password");

        Assert.Equal(EffectiveConfiguration.Redacted, shown.Value);
        Assert.Equal(ConfigurationOrigin.Environment, shown.Origin);
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
