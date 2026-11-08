using Core.Host.Endpoints;
using Core.Host.Operators;
using Core.Host.Screens;
using Core.Host.Tests.Browser.Trial;
using Core.Persistence.Custody;
using Core.Persistence.Migrations;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Configuration;
using Core.Platform.Sessions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>
/// The host of the entry, the start page and the access policy of the screens (issue 594) on a real Kestrel, behind the real session gate and the real screen gate, over a
/// migrated database of its own that holds three people made up for the run, each with the function that opens a different part of the menu: the administration, which reaches
/// the screen of the administration and the audit; the audit, which reaches the audit only; and an annotation, which reaches nothing while no domain is granted. The screens
/// of the test are <see cref="OfAdministration"/>, <see cref="OfAudit"/> and <see cref="OfAnnotation"/>, mapped in the opposite order of their places in the menu, and a route that does
/// not exist, <see cref="Missing"/>. The channel is told to be encrypted, which the loopback of the test cannot be.
/// </summary>
internal sealed class NavigationScreenHost : IAsyncDisposable
{
    public const string OfAdministration = "/zqx-administracao-5940";
    public const string OfAudit = "/zqx-auditoria-5940";
    public const string OfAnnotation = "/zqx-anotacao-5940";
    public const string Missing = "/zqx-nao-existe-5940";
    public const string AdministrationCaption = "Administração";
    public const string AuditCaption = "Auditoria";

    private readonly ScratchDatabase database;
    private WebApplication? app;

    private NavigationScreenHost(ScratchDatabase database)
    {
        this.database = database;
    }

    /// <summary>The password every person of the run has, made up for it.</summary>
    public string Password { get; } = $"zqx-senha-{Guid.NewGuid():N}";

    /// <summary>The person of the function that reaches the screen of the administration and the audit.</summary>
    public string Administrator { get; } = $"zqx-administracao-{Guid.NewGuid():N}";

    /// <summary>The person of the function that reaches the audit only.</summary>
    public string Auditor { get; } = $"zqx-auditoria-{Guid.NewGuid():N}";

    /// <summary>The person of a function that reaches a screen only with a domain granted, and none is.</summary>
    public string Annotator { get; } = $"zqx-anotacao-{Guid.NewGuid():N}";

    public Uri Address { get; private set; } = new("http://127.0.0.1");

    public static async Task<NavigationScreenHost> StartAsync(CancellationToken cancellationToken)
    {
        var database = await ScratchDatabase.CreateAsync();
        try
        {
            _ = await database.MigrateAsync(EmbeddedMigrations.Load());
            var builder = CompositionRoot.ClosedBuilder("http://127.0.0.1:0");
            CompositionRoot.Compose(builder, ClosedConfiguration.Load(new ConfigurationSchema([HostConfiguration.Section])).View, fileLog: null);
            _ = builder.Services.AddSingleton(new SessionStore(new SessionLifetimes(TimeSpan.FromMinutes(30), TimeSpan.FromHours(8)), TimeProvider.System));
            _ = builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(database.AppConnectionString)).AddSingleton(AdministrationOf).AddSingleton(SignInOf);
            var app = builder.Build();
            _ = app.Use((context, next) =>
            {
                context.Request.Scheme = "https";
                return next(context);
            });
            SessionGate.Use(app);
            ScreenGate.Use(app);
            EntryPageEndpoint.Map(app);
            MapScreens(app);
            ScreenAssets.Map(app);
            await app.StartAsync(cancellationToken);
            var host = new NavigationScreenHost(database) { app = app, Address = new Uri(app.Urls.Single()) };
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

    /// <summary>The screens of the test, with their entries at places that are not the order they are mapped in, and one that only a person with a domain reaches.</summary>
    private static void MapScreens(IEndpointRouteBuilder app)
    {
        _ = app.MapGet(OfAnnotation, Show).WithMetadata(ScreenAccess.Of("Anotação", 30, "Anotação"));
        _ = app.MapGet(OfAudit, Show).WithMetadata(ScreenAccess.Of(AuditCaption, 20, PersonFunctions.Audit, PersonFunctions.Administration));
        _ = app.MapGet(OfAdministration, Show).WithMetadata(ScreenAccess.Of(AdministrationCaption, 10, PersonFunctions.Administration));
        NoticeScreensEndpoint.Map(app);
    }

    private static RazorComponentResult<ScreenFrame> Show(HttpContext context) => ScreenResult.Of(context, typeof(BareScreen), "Tela de ensaio");

    internal static OperatorAdministration AdministrationOf(IServiceProvider provider)
    {
        var source = provider.GetRequiredService<NpgsqlDataSource>();
        return new OperatorAdministration(new PersonRegistry(source), new PersonCredentials(source, new PasswordDerivation(1)), provider.GetRequiredService<SessionStore>(), new PersonNotices(source));
    }

    internal static SignIn SignInOf(IServiceProvider provider)
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
