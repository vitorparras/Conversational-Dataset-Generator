using System.Reflection;
using Core.Persistence.Connection;
using Core.Platform.Keys;
using Core.Platform.Startup;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Host.Tests;

/// <summary>
/// The composition root registers the server's checks with a database declared: the guarantees, with the window of the owner credential right after the privilege, then the labels last.
/// </summary>
public sealed class ServerChecksRegistrationTests
{
    private static readonly Dictionary<string, string> Declared = new(StringComparer.Ordinal)
    {
        ["Database:Host"] = "127.0.0.1:5432",
        ["Database:Name"] = "zqx_database",
        ["Database:Username"] = "zqx_app",
        ["Database:Password"] = "ZQX-SENHA-7731",
    };

    [Fact]
    public async Task DeclaredDatabaseRegistersTheGuaranteesInOrderAndTheDerivationLabelsLast()
    {
        await using var provider = RootChecks(DatabaseConfiguration.Read(Declared));

        var names = provider.GetServices<IStartupCheck>().Select(check => check.Name);

        Assert.Equal(
            ["Privilege", "OwnerWindow", "RlsForce", "RestoreWindow", "Serializable", "PgAudit", "ColumnRevoke", "SchemaVersion", "Immutability", "ProtectedColumns", "DerivationLabels"],
            names);
    }

    [Fact]
    public async Task UndeclaredDatabaseRegistersOnlyTheCheckThatSaysNoneIsDeclared()
    {
        await using var provider = RootChecks(DatabaseConfiguration.Read(new Dictionary<string, string>()));

        var names = provider.GetServices<IStartupCheck>().Select(check => check.Name);

        Assert.Equal(["Database"], names);
    }

    private static ServiceProvider RootChecks(DatabaseSettings database)
    {
        var method = typeof(CompositionRoot).GetMethod("AddServerGuarantees", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("CompositionRoot.AddServerGuarantees was not found; the registration test must follow it.");
        var services = new ServiceCollection();
        _ = services.AddSingleton(TimeProvider.System);
        _ = services.AddSingleton<IMasterKeySource>(new FileMasterKeySource("zqx-never-read-7731", "zqx-never-read-7732", "zqx-never-read-7733", AppContext.BaseDirectory));
        _ = method.Invoke(null, [services, database]);
        return services.BuildServiceProvider();
    }
}
