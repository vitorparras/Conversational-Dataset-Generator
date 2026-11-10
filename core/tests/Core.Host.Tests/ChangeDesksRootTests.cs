using Core.Host.Settings;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Core.Host.Tests;

/// <summary>The root builds the parts of the two desks of the changes (#634) over the startup data source,
/// and none when no database was declared: the screens then lead nowhere.</summary>
public sealed class ChangeDesksRootTests
{
    private const string Connection = "Host=localhost;Database=zqx;Username=zqx";

    [Fact]
    public async Task PartsAreBuiltOverTheDataSourceTheStartupContainerHolds()
    {
        await using var source = NpgsqlDataSource.Create(Connection);

        var parts = ChangeParts.Of(Startup(source));

        Assert.NotNull(parts);
        Assert.NotNull(parts.Changes);
        Assert.NotNull(parts.Registry);
    }

    [Fact]
    public void NoPartsAreBuiltWhenNoDatabaseWasDeclared() => Assert.Null(ChangeParts.Of(Startup(source: null)));

    private static ServiceProvider Startup(NpgsqlDataSource? source)
    {
        var startup = new ServiceCollection();
        _ = source is null ? startup : startup.AddSingleton(source);
        var inner = startup.BuildServiceProvider();
        return new ServiceCollection().AddSingleton(inner).BuildServiceProvider();
    }
}
