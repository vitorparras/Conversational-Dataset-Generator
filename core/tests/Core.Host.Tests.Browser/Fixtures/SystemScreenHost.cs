using Core.Boundary.Fake;
using Core.Boundary.Http;
using Core.Host.Endpoints;
using Core.Host.Screens;
using Core.Host.Supervision;
using Core.Persistence.Custody;
using Core.Persistence.Migrations;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Core.Platform;
using Core.Platform.Configuration;
using Core.Platform.Configuration.Sources;
using Core.Platform.Sessions;
using Core.Platform.Startup;
using Core.Platform.Versions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>
/// The host of the screens of the configuration in force and of the state of the system (issues 603 and 604) on a real Kestrel, behind the real session gate and the real screen
/// gate, with the real routes of both screens, over a migrated database of its own that holds three people made up for the run: the administration, the audit and an annotation. The
/// configuration is the one of the host with the keys of the safekeeping copy, one of them secret, written as an argument with a value made up for the run, <see cref="Secret"/>,
/// which no page may show. The startup report is what the test sets. The channel is told to be encrypted, which the loopback of the test cannot be.
/// </summary>
internal sealed class SystemScreenHost : IAsyncDisposable
{
    public const string DeclaredDate = "2026-10-01";
    public const string PassedCheck = "Chave mestra";
    public const string FailedCheck = "Servidor de banco";
    public const string FailedMessage = "a versão do servidor é menor que a exigida";
    public const string FailedObserved = "PostgreSQL 14.2";

    private readonly ScratchDatabase database;
    private WebApplication? app;

    private SystemScreenHost(ScratchDatabase database, string secret)
    {
        this.database = database;
        Secret = secret;
        Configuration = ClosedConfiguration.Load(
            new ConfigurationSchema([HostConfiguration.Section, SafekeepingConfiguration.Section]),
            new StrictArgumentsSource([$"--{SafekeepingConfiguration.HolderPath}={secret}", $"--{SafekeepingConfiguration.LastRecoveryTestPath}={DeclaredDate}"])).View;
    }

    /// <summary>The value of the secret key of the run, which no page may show anywhere.</summary>
    public string Secret { get; }

    /// <summary>The password every person of the run has, made up for it.</summary>
    public string Password { get; } = $"zqx-senha-{Guid.NewGuid():N}";

    /// <summary>The person of the technical administration.</summary>
    public string Administrator { get; } = $"zqx-administracao-{Guid.NewGuid():N}";

    /// <summary>The person of the audit.</summary>
    public string Auditor { get; } = $"zqx-auditoria-{Guid.NewGuid():N}";

    /// <summary>The person of an annotation, who reaches neither screen.</summary>
    public string Annotator { get; } = $"zqx-anotacao-{Guid.NewGuid():N}";

    /// <summary>The configuration in force the screen shows, which the test compares with the page.</summary>
    public EffectiveConfiguration Configuration { get; }

    /// <summary>The startup report the next request of the state reads: one check that passed and one that failed, until a test sets another.</summary>
    public StartupReport Report { get; set; } = new(
        [
            CheckResult.Passed(PassedCheck, "a chave foi lida e confere", "arquivo de chaves do servidor"),
            CheckResult.Failed(FailedCheck, FailedMessage, "servidor de banco da instalação") with { Observed = FailedObserved },
        ],
        new DateTimeOffset(2026, 11, 6, 9, 30, 0, TimeSpan.Zero));

    public Uri Address { get; private set; } = new("http://127.0.0.1");

    public static async Task<SystemScreenHost> StartAsync(CancellationToken cancellationToken)
    {
        var database = await ScratchDatabase.CreateAsync();
        try
        {
            _ = await database.MigrateAsync(EmbeddedMigrations.Load());
            var host = new SystemScreenHost(database, $"zqx-segredo-{Guid.NewGuid():N}");
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

        await database.DisposeAsync();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var builder = CompositionRoot.ClosedBuilder("http://127.0.0.1:0");
        CompositionRoot.Compose(builder, Configuration, fileLog: null);
        _ = builder.Services.AddSingleton(new SessionStore(new SessionLifetimes(TimeSpan.FromMinutes(30), TimeSpan.FromHours(8)), TimeProvider.System));
        _ = builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(database.AppConnectionString))
            .AddSingleton(NavigationScreenHost.AdministrationOf)
            .AddSingleton(NavigationScreenHost.SignInOf);
        _ = builder.Services.AddTransient(_ => Report)
            .AddSingleton(new ServiceProcessWatch())
            .AddSingleton(new Installation(InstallationKind.Demonstration))
            .AddSingleton<AuditComparisonState>()
            .AddSingleton<SealWarningState>()
            .AddSingleton(new VersionReport(new BuildInfo(VersionValue.Present("9.8.7"), VersionValue.Present("0123456789abcdef0123456789abcdef01234567"), WorkingTree.Clean))
                .WithSchemaVersion(VersionValue.Present("0042")))
            .AddSingleton<IServiceVersionReader, NoServiceVersionReader>();
        var started = builder.Build();
        _ = started.Use((context, next) =>
        {
            context.Request.Scheme = "https";
            return next(context);
        });
        SessionGate.Use(started);
        ScreenGate.Use(started);
        EntryPageEndpoint.Map(started);
        SystemScreensEndpoint.Map(started, RecognizerKind.Fake);
        ScreenAssets.Map(started);
        await started.StartAsync(cancellationToken);
        app = started;
        Address = new Uri(started.Urls.Single());
    }

    private async Task AddPeopleAsync(CancellationToken cancellationToken)
    {
        await using var source = NpgsqlDataSource.Create(database.AppConnectionString);
        var registry = new PersonRegistry(source);
        var credentials = new PersonCredentials(source, new PasswordDerivation(1));
        foreach (var (name, function) in new[] { (Administrator, PersonFunctions.Administration), (Auditor, PersonFunctions.Audit), (Annotator, PersonFunctions.Accepted[0]) })
        {
            var created = await registry.CreateAsync(new NewPerson(name, function), CustodyAuthor.System, cancellationToken);
            _ = await credentials.SetPasswordAsync(created.PersonId!.Value, Password, CustodyAuthor.System, cancellationToken);
        }
    }
}
