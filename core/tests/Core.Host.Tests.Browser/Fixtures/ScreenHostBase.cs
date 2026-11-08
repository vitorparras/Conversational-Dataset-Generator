using Core.Host.Endpoints;
using Core.Host.Screens;
using Core.Persistence.Custody;
using Core.Persistence.Migrations;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Configuration;
using Core.Platform.Sessions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>
/// A real Kestrel behind the real session gate and the real screen gate, over a migrated database of its own that holds four people made up for the run, each with a function that
/// opens a different part of the menu: the technical administration, the audit, the curation and an annotation, the last two with no domain granted. The screens a test maps are
/// the real routes of the host; the channel is told to be encrypted, which the loopback of the test cannot be.
/// </summary>
internal abstract class ScreenHostBase : IAsyncDisposable
{
    private WebApplication? app;

    protected ScreenHostBase(ScratchDatabase database)
    {
        Database = database;
    }

    /// <summary>The password every person of the run has, made up for it.</summary>
    public string Password { get; } = $"ZQX-SENHA-{Guid.NewGuid():N}";

    /// <summary>The person of the technical administration.</summary>
    public string Administrator { get; } = $"zqx-administracao-{Guid.NewGuid():N}";

    /// <summary>The person of the audit.</summary>
    public string Auditor { get; } = $"zqx-auditoria-{Guid.NewGuid():N}";

    /// <summary>The person of the curation of the collection, with no domain granted.</summary>
    public string Curator { get; } = $"zqx-curadoria-{Guid.NewGuid():N}";

    /// <summary>The person of an annotation, with no domain granted.</summary>
    public string Annotator { get; } = $"zqx-anotacao-{Guid.NewGuid():N}";

    public Uri Address { get; private set; } = new("http://127.0.0.1");

    protected ScratchDatabase Database { get; }

    /// <summary>The host, migrated and with its people, once <paramref name="prepare"/> has written what the test needs in the database.</summary>
    protected static async Task<T> StartAsync<T>(Func<ScratchDatabase, T> make, Func<T, Task> prepare, CancellationToken cancellationToken)
        where T : ScreenHostBase
    {
        var database = await ScratchDatabase.CreateAsync();
        try
        {
            _ = await database.MigrateAsync(EmbeddedMigrations.Load());
            var host = make(database);
            await prepare(host);
            await host.RunAsync(cancellationToken);
            await host.AddPeopleAsync(cancellationToken);
            return host;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (app is not null)
        {
            await app.DisposeAsync();
        }

        await Database.DisposeAsync();
    }

    /// <summary>The services the screens read, besides the session, the people and the data source.</summary>
    protected abstract void AddServices(IServiceCollection services);

    /// <summary>The routes of the screens the test reaches.</summary>
    protected abstract void MapScreens(IEndpointRouteBuilder routes);

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var builder = CompositionRoot.ClosedBuilder("http://127.0.0.1:0");
        CompositionRoot.Compose(builder, ClosedConfiguration.Load(new ConfigurationSchema([HostConfiguration.Section])).View, fileLog: null);
        _ = builder.Services.AddSingleton(new SessionStore(new SessionLifetimes(TimeSpan.FromMinutes(30), TimeSpan.FromHours(8)), TimeProvider.System));
        _ = builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(Database.AppConnectionString))
            .AddSingleton(NavigationScreenHost.AdministrationOf)
            .AddSingleton(NavigationScreenHost.SignInOf);
        AddServices(builder.Services);
        var started = builder.Build();
        _ = started.Use((context, next) =>
        {
            context.Request.Scheme = "https";
            return next(context);
        });
        SessionGate.Use(started);
        ScreenGate.Use(started);
        EntryPageEndpoint.Map(started);
        MapScreens(started);
        ScreenAssets.Map(started);
        await started.StartAsync(cancellationToken);
        app = started;
        Address = new Uri(started.Urls.Single());
    }

    private async Task AddPeopleAsync(CancellationToken cancellationToken)
    {
        await using var source = NpgsqlDataSource.Create(Database.AppConnectionString);
        var registry = new PersonRegistry(source);
        var credentials = new PersonCredentials(source, new PasswordDerivation(1));
        foreach (var (name, function) in new[]
        {
            (Administrator, PersonFunctions.Administration),
            (Auditor, PersonFunctions.Audit),
            (Curator, PersonFunctions.Curation),
            (Annotator, PersonFunctions.Accepted[0]),
        })
        {
            var created = await registry.CreateAsync(new NewPerson(name, function), CustodyAuthor.System, cancellationToken);
            _ = await credentials.SetPasswordAsync(created.PersonId!.Value, Password, CustodyAuthor.System, cancellationToken);
        }
    }
}
