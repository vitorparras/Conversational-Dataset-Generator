using Core.Boundary.Fake;
using Core.Boundary.Http;
using Core.Host.Endpoints;
using Core.Host.Operators;
using Core.Host.Supervision;
using Core.Persistence.Custody;
using Core.Persistence.Domains;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Core.Platform;
using Core.Platform.Sessions;
using Core.Platform.Startup;
using Core.Platform.Versions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>
/// The host of every screen of phase 3 that the main has, with its real route behind the real gates, over one migrated database of its own: the account, the people, the state of the
/// system, the configuration, the domains, the chain and the notices. The four people of the run are there, and so is <see cref="Noticed"/>, an audit with two unread notices; the
/// chain has two verifications on record and two domains are listed, so that every table has rows. It is the place for the checks that have to cross every screen, which the host of
/// one screen cannot do.
/// </summary>
internal sealed class PhaseThreeScreenHost : ScreenHostBase
{
    private const string TwoDomains = "INSERT INTO public.domain (name) VALUES ('ZQX-dominio-alfa'), ('ZQX-dominio-beta')";

    private PhaseThreeScreenHost(ScratchDatabase database)
        : base(database)
    {
    }

    /// <summary>The audit with two unread notices, made for the run.</summary>
    public string Noticed { get; } = $"zqx-avisada-{Guid.NewGuid():N}";

    public static Task<PhaseThreeScreenHost> StartAsync(CancellationToken cancellationToken) =>
        StartAsync(database => new PhaseThreeScreenHost(database), host => host.Database.RunAsOwnerAsync(TwoDomains), host => host.FinishAsync(cancellationToken), cancellationToken);

    protected override void AddServices(IServiceCollection services)
    {
        var startup = new ServiceCollection().AddSingleton(_ => NpgsqlDataSource.Create(Database.AppConnectionString)).BuildServiceProvider();
        _ = services.AddSingleton(startup)
            .AddSingleton(new VerificationSchedule(TimeSpan.FromHours(1)))
            .AddSingleton(new SealDeclaration(SealSituation.External, null))
            .AddSingleton<SealWarningState>()
            .AddSingleton(provider => new ChainVerificationService(provider))
            .AddSingleton(provider => new DomainListing(provider.GetRequiredService<NpgsqlDataSource>()))
            .AddSingleton(new SignInDelay(TimeProvider.System))
            .AddSingleton(provider => new PasswordChange(
                provider.GetRequiredService<OperatorAdministration>().Credentials, provider.GetRequiredService<SessionStore>(), provider.GetRequiredService<SignInDelay>()));
        _ = services.AddTransient(_ => Report)
            .AddSingleton(new ServiceProcessWatch())
            .AddSingleton(new Installation(InstallationKind.Real))
            .AddSingleton<AuditComparisonState>()
            .AddSingleton(new VersionReport(new BuildInfo(VersionValue.Present("9.8.7"), VersionValue.Present("0123456789abcdef0123456789abcdef01234567"), WorkingTree.Clean))
                .WithSchemaVersion(VersionValue.Present("0042")))
            .AddSingleton<IServiceVersionReader, NoServiceVersionReader>();
    }

    protected override void MapScreens(IEndpointRouteBuilder routes)
    {
        AccountScreensEndpoint.Map(routes);
        NoticeScreensEndpoint.Map(routes);
        OperatorScreensEndpoint.Map(routes);
        SystemScreensEndpoint.Map(routes, RecognizerKind.Fake);
        DomainScreensEndpoint.Map(routes);
        ChainScreensEndpoint.Map(routes);
    }

    private static StartupReport Report { get; } = new(
        [
            CheckResult.Passed("Chave mestra", "a chave foi lida e confere", "arquivo de chaves do servidor"),
            CheckResult.Passed("Esquema do banco", "a versão do esquema é a esperada", "migrações embutidas"),
        ],
        new DateTimeOffset(2026, 11, 6, 9, 30, 0, TimeSpan.Zero));

    private async Task FinishAsync(CancellationToken cancellationToken)
    {
        var chain = Services.GetRequiredService<ChainVerificationService>();
        for (var verification = 0; verification < 2; verification++)
        {
            Assert.NotNull(await chain.VerifyAsync(seal: null, requestedBy: null, cancellationToken));
        }

        var source = NpgsqlDataSource.Create(Database.AppConnectionString);
        await using (source.ConfigureAwait(false))
        {
            var created = await new PersonRegistry(source).CreateAsync(new NewPerson(Noticed, PersonFunctions.Audit), CustodyAuthor.System, cancellationToken);
            var id = created.PersonId!.Value;
            _ = await new PersonCredentials(source, new PasswordDerivation(1)).SetPasswordAsync(id, Password, CustodyAuthor.System, cancellationToken);
            var notices = new PersonNotices(source);
            await notices.RecordPasswordResetAsync(id, 2, cancellationToken);
            await notices.RecordPasswordResetAsync(id, 0, cancellationToken);
        }
    }
}
