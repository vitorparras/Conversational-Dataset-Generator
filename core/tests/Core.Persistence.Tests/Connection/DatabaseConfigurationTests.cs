using Core.Persistence.Connection;
using Core.Platform.Configuration;
using Core.Platform.Configuration.Sources;
using Npgsql;

namespace Core.Persistence.Tests.Connection;

/// <summary>
/// The application's credential comes from the closed configuration: all four keys are required, and the port of the host must be a number; the password is never shown.
/// </summary>
public sealed class DatabaseConfigurationTests
{
    private const string Password = "ZQX-SENHA-7731";

    [Fact]
    public void ConnectionStringCarriesTheConfiguredServerDatabaseAndCredential()
    {
        var settings = DatabaseConfiguration.Read(Declared(Complete()));

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

        var settings = DatabaseConfiguration.Read(Declared(variables));

        Assert.Equal(DatabaseSettingsState.Configured, settings.State);
    }

    [Fact]
    public void NoKeyDeclaredIsRefusedAtLoadNamingAllFour()
    {
        var loaded = Load([]);

        Assert.True(loaded.Refused);
        Assert.Equal(
            ["Database:Host", "Database:Name", "Database:Username", "Database:Password"],
            loaded.Problems.Where(problem => problem.Kind == ConfigurationProblemKind.MissingKey).Select(problem => problem.Key));
    }

    [Fact]
    public void NoKeyDeclaredIsRefusedNamingAllFour()
    {
        var settings = DatabaseConfiguration.Read(Declared([]));

        Assert.Equal(DatabaseSettingsState.Refused, settings.State);
        Assert.Equal(
            ["Database:Host", "Database:Name", "Database:Username", "Database:Password"], settings.Problems.Select(problem => problem.Key));
    }

    [Fact]
    public void CompanionsWithoutTheHostAreRefusedNamingTheHost()
    {
        var variables = Complete();
        _ = variables.Remove("CDG_Database__Host");

        var settings = DatabaseConfiguration.Read(Declared(variables));

        Assert.Equal(DatabaseSettingsState.Refused, settings.State);
        var problem = Assert.Single(settings.Problems);
        Assert.Equal(ConfigurationProblemKind.MissingKey, problem.Kind);
        Assert.Equal("Database:Host", problem.Key);
    }

    [Theory]
    [InlineData("CDG_Database__Name", "Database:Name")]
    [InlineData("CDG_Database__Username", "Database:Username")]
    [InlineData("CDG_Database__Password", "Database:Password")]
    public void HostWithoutACompanionIsRefusedNamingIt(string variable, string key)
    {
        var variables = Complete();
        _ = variables.Remove(variable);

        var settings = DatabaseConfiguration.Read(Declared(variables));

        var problem = Assert.Single(settings.Problems);
        Assert.Equal(ConfigurationProblemKind.MissingKey, problem.Kind);
        Assert.Equal(key, problem.Key);
    }

    [Fact]
    public void SingleCompanionIsRefusedNamingTheOtherThree()
    {
        var settings = DatabaseConfiguration.Read(Declared(new(StringComparer.Ordinal) { ["CDG_Database__Name"] = "zqx_database" }));

        Assert.Equal(["Database:Host", "Database:Username", "Database:Password"], settings.Problems.Select(problem => problem.Key));
    }

    [Fact]
    public void HostWithoutThePasswordIsRefusedNamingTheKey()
    {
        var variables = Complete();
        _ = variables.Remove("CDG_Database__Password");

        var settings = DatabaseConfiguration.Read(Declared(variables));

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
    [InlineData("zqx-host:+5432")]
    [InlineData("zqx-host:zqx,zqx-host:1")]
    [InlineData("zqx-host,zqx-other")]
    [InlineData("::1")]
    [InlineData("[::1]:5432")]
    [InlineData(":5432")]
    [InlineData(" :5432")]
    public void HostThatIsNotOneServerWithAPortIsRefusedNamingTheHostKey(string host)
    {
        var variables = Complete();
        variables["CDG_Database__Host"] = host;

        var settings = DatabaseConfiguration.Read(Declared(variables));

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

        var settings = DatabaseConfiguration.Read(Declared(variables));

        Assert.Equal(DatabaseSettingsState.Configured, settings.State);
    }

    [Theory]
    [InlineData("CDG_Database__OwnerUsername", "Database:OwnerUsername")]
    [InlineData("CDG_Database__OwnerPassword", "Database:OwnerPassword")]
    [InlineData("CDG_Database__Owner__Username", "Database:Owner:Username")]
    public void NormalModeOffersNoKeyForASecondCredentialAndRefusesOneThatIsGiven(string variable, string key)
    {
        var variables = Complete();
        variables[variable] = "zqx-owner";

        var loaded = Load(variables);

        var problem = Assert.Single(loaded.Problems);
        Assert.Equal((ConfigurationProblemKind.UnknownKey, key), (problem.Kind, problem.Key));
    }

    [Fact]
    public void PasswordIsRedactedInTheConfigurationInForce()
    {
        var loaded = Load(Complete());

        var shown = Assert.Single(loaded.View.Entries, entry => entry.Key == "Database:Password");

        Assert.Equal(EffectiveConfiguration.Redacted, shown.Value);
        Assert.Equal(ConfigurationOrigin.Environment, shown.Origin);
    }

    /// <summary>The values a load of the whole section would hand over, by key, for the reading of one that is missing or wrong.</summary>
    private static Dictionary<string, string> Declared(Dictionary<string, string?> variables) =>
        variables.ToDictionary(
            variable => variable.Key.Replace("CDG_", string.Empty, StringComparison.Ordinal).Replace("__", ":", StringComparison.Ordinal),
            variable => variable.Value!,
            StringComparer.Ordinal);

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
