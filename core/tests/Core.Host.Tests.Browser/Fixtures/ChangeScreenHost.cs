using Core.Host.Endpoints;
using Core.Host.Settings;
using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Persistence.Settings;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Configuration;
using Core.Tests.Support.Audit;
using Core.Tests.Support.Custody;
using Core.Tests.Support.Net;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>
/// The host of the screen of the changes of configuration (#634, part 2c) with the real route, the real desks over the database and a configuration in force the start takes, so that the
/// trial of a change runs the rules of the start. Besides the people of every host, a second person of the technical administration decides what the first one proposes.
/// </summary>
internal sealed class ChangeScreenHost : ScreenHostBase
{
    private ChangeScreenHost(ScratchDatabase database)
        : base(database)
    {
    }

    /// <summary>A second person of the technical administration, with the password of the run.</summary>
    public string SecondAdministrator { get; } = $"zqx-administracao-2-{Guid.NewGuid():N}";

    public static Task<ChangeScreenHost> StartAsync(CancellationToken cancellationToken) =>
        StartAsync(database => new ChangeScreenHost(database), _ => Task.CompletedTask, host => host.AddSecondAsync(), cancellationToken);

    /// <summary>A proposal of the first administrator, written as the screen writes it, so that a test finds it waiting.</summary>
    public async Task<long> ProposeAsync(string value)
    {
        await using var source = NpgsqlDataSource.Create(Database.AppConnectionString);
        var author = (await new PersonRegistry(source).IdOfAsync(Administrator, CancellationToken.None))!.Value;
        var written = await new ConfigurationChanges(source).SubmitAsync(
            new ConfigurationSubmission { Key = "Session:IdleTimeout", Value = value, Motive = "operational_adjustment", AuthorId = author }, CancellationToken.None);
        return written.ChangeId!.Value;
    }

    /// <summary>A change of the first administrator approved by the second and recorded by the core as never to be applied, for the reason given; the key of the change.</summary>
    public async Task<string> RefuseAsync(string reasonCode)
    {
        var id = await ProposeAsync("00:25:00");
        await using var source = NpgsqlDataSource.Create(Database.AppConnectionString);
        var changes = new ConfigurationChanges(source);
        var decider = (await new PersonRegistry(source).IdOfAsync(SecondAdministrator, CancellationToken.None))!.Value;
        _ = await changes.DecideAsync(decider, (await changes.SubmittedAsync(id, CancellationToken.None))!, approved: true, CancellationToken.None);
        var approved = new ApprovedChanges(source);
        var found = (await approved.PendingAsync(CancellationToken.None)).Single(change => change.Id == id);
        _ = await approved.RecordNotAppliedAsync(found, reasonCode, CancellationToken.None);
        return found.Key;
    }

    protected override void AddServices(IServiceCollection services)
    {
        var inForce = InForce();
        _ = services
            .AddSingleton(provider => new ConfigurationChangeDesk(inForce, Changes(provider), new PersonRegistry(provider.GetRequiredService<NpgsqlDataSource>())))
            .AddSingleton(provider => new ChangeDecisionDesk(inForce, Changes(provider), new PersonRegistry(provider.GetRequiredService<NpgsqlDataSource>()), TimeProvider.System))
            .AddSingleton(provider => new ApprovedChanges(provider.GetRequiredService<NpgsqlDataSource>()));
    }

    protected override void MapScreens(IEndpointRouteBuilder routes) => ConfigurationChangeScreensEndpoint.Map(routes);

    /// <summary>A configuration in force the start takes, with synthetic values and the shared material of the tests, read as the file hands it over.</summary>
    private static ConfigurationLoadResult InForce()
    {
        string[] arguments =
        [
            "--Recognizer:Kind=fake", "--Passwords:WorkFactor=1", "--Session:IdleTimeout=00:30:00", "--Session:AbsoluteTimeout=08:00:00",
            "--Ingestion:Conditions=restrita,intermediaria,permissiva", "--Custody:VerificationInterval=00:05:00", "--Custody:SealSituation=Manual",
            $"--Tls:CertificateFile={SharedTlsMaterial.CertificatePath}", $"--Tls:KeyFile={SharedTlsMaterial.KeyPath}",
            $"--Audit:ServerLogDirectory={ServerLogFiles.Shared}", "--Audit:CompareInterval=01:00:00", "--Installation:Kind=demonstracao",
            $"--MasterKey:File={CustodyFiles.Shared.KeyPath}", $"--MasterKey:LabelsFile={CustodyFiles.Shared.LabelsPath}", $"--MasterKey:EpochsFile={CustodyFiles.Shared.EpochsPath}",
            "--Database:Host=127.0.0.1:5432", "--Database:Name=zqx", "--Database:Username=zqx_app", "--Database:Password=ZQX-SENHA-6344",
        ];
        var values = arguments.Select(argument => argument[2..].Split('=', 2)).ToDictionary(pair => pair[0], pair => pair[1], StringComparer.Ordinal);
        return ClosedConfiguration.Load(HostConfiguration.Schema(migrating: false, comparing: false), new TrialSource(values, "Session:IdleTimeout", "00:30:00"));
    }

    private static ConfigurationChanges Changes(IServiceProvider provider) => new(provider.GetRequiredService<NpgsqlDataSource>());

    private async Task AddSecondAsync()
    {
        await using var source = NpgsqlDataSource.Create(Database.AppConnectionString);
        var created = await new PersonRegistry(source).CreateAsync(new NewPerson(SecondAdministrator, PersonFunctions.Administration), CustodyAuthor.System, CancellationToken.None);
        _ = await new PersonCredentials(source, new PasswordDerivation(1)).SetPasswordAsync(created.PersonId!.Value, Password, CustodyAuthor.System, CancellationToken.None);
    }
}
