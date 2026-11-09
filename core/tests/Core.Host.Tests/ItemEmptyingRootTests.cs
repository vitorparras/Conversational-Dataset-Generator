using Core.Host.Endpoints;
using Core.Platform.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Core.Host.Tests;

/// <summary>The root registers what the screens of the emptying need (#617), so that the routes only ask for the desk and the source of grants.</summary>
public sealed class ItemEmptyingRootTests
{
    private const string Connection = "Host=localhost;Database=zqx;Username=zqx;Password=zqx";

    [Fact]
    public async Task RootResolvesTheDeskOnceWhenTheStartupContainerHoldsTheDataSource()
    {
        await using var source = NpgsqlDataSource.Create(Connection);
        await using var app = Composed(source);

        var first = app.Services.GetRequiredService<ItemEmptyingDesk>();
        var second = app.Services.GetRequiredService<ItemEmptyingDesk>();

        Assert.Same(first, second);
    }

    [Fact]
    public async Task RootResolvesNoDeskWhenNoDatabaseWasDeclared()
    {
        await using var app = Composed(source: null);

        Assert.Null(app.Services.GetService<ItemEmptyingDesk>());
    }

    [Fact]
    public async Task RootGrantsNoDomainToAnyoneUntilTheRealSourceTakesItsPlace()
    {
        await using var app = Composed(source: null);

        var granted = await app.Services.GetRequiredService<DomainGrants>().GrantedToAsync(7L, TestContext.Current.CancellationToken);

        Assert.Empty(granted);
    }

    [Fact]
    public void FactoriesRefuseAMissingContainer()
    {
        _ = Assert.Throws<ArgumentNullException>(() => ItemEmptyingServices.DeskOf(null!));
        _ = Assert.Throws<ArgumentNullException>(() => ItemEmptyingServices.GrantsOf(null!));
    }

    private static WebApplication Composed(NpgsqlDataSource? source)
    {
        var startup = new ServiceCollection();
        _ = source is null ? startup : startup.AddSingleton(source);
        var builder = CompositionRoot.ClosedBuilder(HostProcess.FreePlainAddress().GetLeftPart(UriPartial.Authority));
        CompositionRoot.Compose(builder, ClosedConfiguration.Load(new ConfigurationSchema([HostConfiguration.Section])).View, fileLog: null);
        _ = builder.Services.AddSingleton(startup.BuildServiceProvider());
        return builder.Build();
    }
}
