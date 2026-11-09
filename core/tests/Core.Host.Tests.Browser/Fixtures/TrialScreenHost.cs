using Core.Host.Endpoints;
using Core.Host.Screens;
using Core.Host.Tests.Browser.Trial;
using Core.Persistence.Migrations;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    private readonly HostLog log;

    private TrialScreenHost(ScratchDatabase database, WebApplication app, HostLog log)
    {
        this.database = database;
        this.app = app;
        this.log = log;
        Address = new Uri(app.Urls.Single());
    }

    /// <summary>The address the server listens on, with the port it was given.</summary>
    public Uri Address { get; }

    /// <summary>The lines the host logged, with the id of their event.</summary>
    public IReadOnlyList<string> Log => log.Lines;

    public static async Task<TrialScreenHost> StartAsync(CancellationToken cancellationToken)
    {
        var database = await ScratchDatabase.CreateAsync();
        try
        {
            _ = await database.MigrateAsync(EmbeddedMigrations.Load());
            var log = new HostLog();
            var builder = CompositionRoot.ClosedBuilder("http://127.0.0.1:0");
            _ = builder.Services.AddLogging(logging => logging.AddProvider(log));
            CompositionRoot.Compose(builder, ClosedConfiguration.Load(new ConfigurationSchema([HostConfiguration.Section])).View, fileLog: null);
            _ = builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(database.AppConnectionString));
            var app = builder.Build();
            ScreenFailure.Use(app);
            ScreenAssets.Map(app);
            TrialRoutes.Map(app);
            await app.StartAsync(cancellationToken);
            return new TrialScreenHost(database, app, log);
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
