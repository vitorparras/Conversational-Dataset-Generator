using Core.Host.Screens;
using Core.Host.Tests.Browser.Trial;
using Core.Persistence.Migrations;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>
/// The host composition on a real Kestrel, on a loopback port the system gives, over a migrated database of its own, with the routes of the screens
/// and the ones that exist for the test alone. A browser reaches it by <see cref="Address"/>; the database and the server go with the object.
/// </summary>
internal sealed class TrialScreenHost : IAsyncDisposable
{
    private readonly ScratchDatabase database;
    private readonly WebApplication app;

    private TrialScreenHost(ScratchDatabase database, WebApplication app)
    {
        this.database = database;
        this.app = app;
        Address = new Uri(app.Urls.Single());
    }

    /// <summary>The address the server listens on, with the port it was given.</summary>
    public Uri Address { get; }

    public static async Task<TrialScreenHost> StartAsync(CancellationToken cancellationToken)
    {
        var database = await ScratchDatabase.CreateAsync();
        try
        {
            _ = await database.MigrateAsync(EmbeddedMigrations.Load());
            var builder = CompositionRoot.ClosedBuilder("http://127.0.0.1:0");
            CompositionRoot.Compose(builder, ClosedConfiguration.Load(new ConfigurationSchema([HostConfiguration.Section])).View, fileLog: null);
            _ = builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(database.AppConnectionString));
            var app = builder.Build();
            ScreenAssets.Map(app);
            TrialRoutes.Map(app);
            await app.StartAsync(cancellationToken);
            return new TrialScreenHost(database, app);
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await app.DisposeAsync();
        await database.DisposeAsync();
    }
}
