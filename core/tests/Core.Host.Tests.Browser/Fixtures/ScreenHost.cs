using Core.Host.Endpoints;
using Core.Host.Operators;
using Core.Host.Screens;
using Core.Persistence.Custody;
using Core.Persistence.Migrations;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Configuration;
using Core.Platform.Sessions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>
/// A real Kestrel over a migrated database of its own, put together from the <see cref="ScreenParts"/> of each case: the services and the screens it reaches, and what it writes in the
/// database before and after the start. By default it stands behind the real session gate and the real screen gate and holds four people made up for the run, each with a function that
/// opens a different part of the menu: the technical administration, the audit, the curation and an annotation, the last two with no domain granted. The channel is told to be encrypted,
/// which the loopback of the test cannot be.
/// </summary>
internal sealed class ScreenHost : IAsyncDisposable
{
    private readonly WebApplication app;
    private readonly Action disposing;
    private readonly ScreenPeople people;

    private ScreenHost(ScratchDatabase database, ScreenPeople people, WebApplication app, Action disposing)
    {
        Database = database;
        this.people = people;
        this.app = app;
        this.disposing = disposing;
        Address = new Uri(app.Urls.Single());
    }

    /// <summary>The password every person of the run has, made up for it.</summary>
    public string Password => people.Password;

    /// <summary>The person of the technical administration.</summary>
    public string Administrator => people.Administrator;

    /// <summary>The person of the audit.</summary>
    public string Auditor => people.Auditor;

    /// <summary>The person of the curation of the collection, with no domain granted.</summary>
    public string Curator => people.Curator;

    /// <summary>The person of an annotation, with no domain granted.</summary>
    public string Annotator => people.Annotator;

    /// <summary>The address the server listens on, with the port it was given.</summary>
    public Uri Address { get; }

    /// <summary>The database of the run, migrated.</summary>
    public ScratchDatabase Database { get; }

    /// <summary>The identifier of <see cref="Administrator"/>, once the host has made its people.</summary>
    public long AdministratorId { get; private set; }

    /// <summary>The identifier of <see cref="Curator"/>, once the host has made its people.</summary>
    public long CuratorId { get; private set; }

    /// <summary>The services of the running host, as a screen asks for them.</summary>
    public IServiceProvider Services => app.Services;

    /// <summary>The host, migrated, with <see cref="ScreenParts.Prepare"/> written before the start and <see cref="ScreenParts.Finish"/> run after its people exist.</summary>
    public static Task<ScreenHost> StartAsync(ScreenParts parts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parts);
        return StartCoreAsync(parts, cancellationToken);
    }

    /// <summary>A person with the password of the run, made up for it; the host makes the four of its menu this way, and so does a case that needs one of its own. Gives back the identifier.</summary>
    public async Task<long> AddPersonAsync(string name, string function, CancellationToken cancellationToken)
    {
        await using var source = NpgsqlDataSource.Create(Database.AppConnectionString);
        var created = await new PersonRegistry(source).CreateAsync(new NewPerson(name, function), CustodyAuthor.System, cancellationToken);
        var id = created.PersonId!.Value;
        _ = await new PersonCredentials(source, new PasswordDerivation(1)).SetPasswordAsync(id, Password, CustodyAuthor.System, cancellationToken);
        return id;
    }

    public async ValueTask DisposeAsync()
    {
        disposing();
        await app.DisposeAsync();
        await Database.DisposeAsync();
    }

    private static async Task<ScreenHost> StartCoreAsync(ScreenParts parts, CancellationToken cancellationToken)
    {
        var database = await ScratchDatabase.CreateAsync();
        try
        {
            _ = await database.MigrateAsync(EmbeddedMigrations.Load());
            await parts.Prepare(database);
            var people = new ScreenPeople();
            var host = new ScreenHost(database, people, await RunAsync(parts, new ScreenSetup(database, people), cancellationToken), parts.Disposing);
            try
            {
                if (parts.WithPeople)
                {
                    await host.AddPeopleAsync(cancellationToken);
                }

                await parts.Finish(host);
                return host;
            }
            catch
            {
                await host.DisposeAsync();
                throw;
            }
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    private static async Task<WebApplication> RunAsync(ScreenParts parts, ScreenSetup setup, CancellationToken cancellationToken)
    {
        var builder = CompositionRoot.ClosedBuilder("http://127.0.0.1:0");
        CompositionRoot.Compose(builder, parts.Configuration ?? ClosedConfiguration.Load(new ConfigurationSchema([HostConfiguration.Section])).View, fileLog: null);
        _ = builder.Services.AddSingleton(new SessionStore(new SessionLifetimes(TimeSpan.FromMinutes(30), TimeSpan.FromHours(8)), TimeProvider.System));
        _ = builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(setup.Database.AppConnectionString))
            .AddSingleton(AdministrationOf)
            .AddSingleton(SignInOf);
        parts.Services(builder.Services, setup);
        var started = builder.Build();
        (parts.Pipeline ?? StandardPipeline)(started, parts);
        await started.StartAsync(cancellationToken);
        return started;
    }

    /// <summary>The way a screen is reached: the channel told to be encrypted, the session gate, the screen gate, the entry, the screens of the case and the assets.</summary>
    internal static void StandardPipeline(WebApplication started, ScreenParts parts)
    {
        _ = started.Use((context, next) =>
        {
            context.Request.Scheme = "https";
            return next(context);
        });
        SessionGate.Use(started);
        ScreenGate.Use(started);
        EntryPageEndpoint.Map(started);
        parts.Screens(started);
        ScreenAssets.Map(started);
    }

    private static OperatorAdministration AdministrationOf(IServiceProvider provider)
    {
        var source = provider.GetRequiredService<NpgsqlDataSource>();
        return new OperatorAdministration(new PersonRegistry(source), new PersonCredentials(source, new PasswordDerivation(1)), provider.GetRequiredService<SessionStore>(), new PersonNotices(source));
    }

    private static SignIn SignInOf(IServiceProvider provider)
    {
        var people = provider.GetRequiredService<OperatorAdministration>();
        return new SignIn(
            people.Registry,
            people.Credentials,
            provider.GetRequiredService<SessionStore>(),
            new SignInDelay(TimeProvider.System),
            new SignInRecord(new SignInAttempts(provider.GetRequiredService<NpgsqlDataSource>()), provider.GetRequiredService<ILoggerFactory>()));
    }

    private async Task AddPeopleAsync(CancellationToken cancellationToken)
    {
        AdministratorId = await AddPersonAsync(Administrator, PersonFunctions.Administration, cancellationToken);
        _ = await AddPersonAsync(Auditor, PersonFunctions.Audit, cancellationToken);
        CuratorId = await AddPersonAsync(Curator, PersonFunctions.Curation, cancellationToken);
        _ = await AddPersonAsync(Annotator, PersonFunctions.Accepted[0], cancellationToken);
    }
}
