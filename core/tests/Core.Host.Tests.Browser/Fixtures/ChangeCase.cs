using System.Diagnostics.CodeAnalysis;
using Core.Host.Endpoints;
using Core.Host.Settings;
using Core.Persistence.People;
using Core.Persistence.Settings;
using Core.Platform.Configuration;
using Core.Tests.Support.Audit;
using Core.Tests.Support.Custody;
using Core.Tests.Support.Net;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>
/// The case of the screen of the changes of configuration (#634, part 2c) with the real route, the real desks over the database and a configuration in force the start takes, so that the
/// trial of a change runs the rules of the start. Besides the people of every host, a second person of the technical administration decides what the first one proposes.
/// </summary>
internal sealed class ChangeCase : IAsyncDisposable
{
    private ChangeCase(ScreenHost host, string secondAdministrator, long secondAdministratorId)
    {
        Host = host;
        SecondAdministrator = secondAdministrator;
        SecondAdministratorId = secondAdministratorId;
    }

    /// <summary>The host of the case.</summary>
    public ScreenHost Host { get; }

    /// <summary>A second person of the technical administration, with the password of the run.</summary>
    public string SecondAdministrator { get; }

    /// <summary>The identifier of <see cref="SecondAdministrator"/>.</summary>
    public long SecondAdministratorId { get; }

    [SuppressMessage("Reliability", "CA2000", Justification = "The host goes to the case that is returned, which disposes it.")]
    public static async Task<ChangeCase> StartAsync(CancellationToken cancellationToken)
    {
        var second = $"zqx-administracao-2-{Guid.NewGuid():N}";
        var inForce = InForce();
        var secondId = 0L;
        var host = await ScreenHost.StartAsync(
            new ScreenParts
            {
                Services = (services, _) => services
                    .AddSingleton(provider => new ConfigurationChangeDesk(inForce, Changes(provider), new PersonRegistry(provider.GetRequiredService<NpgsqlDataSource>())))
                    .AddSingleton(provider => new ChangeDecisionDesk(inForce, Changes(provider), new PersonRegistry(provider.GetRequiredService<NpgsqlDataSource>()), TimeProvider.System)),
                Screens = ConfigurationChangeScreensEndpoint.Map,
                Finish = async started => secondId = await started.AddPersonAsync(second, PersonFunctions.Administration, cancellationToken),
            },
            cancellationToken);
        return new ChangeCase(host, second, secondId);
    }

    /// <summary>A proposal of the first administrator, written as the screen writes it, so that a test finds it waiting.</summary>
    public async Task<long> ProposeAsync(string value)
    {
        await using var source = NpgsqlDataSource.Create(Host.Database.AppConnectionString);
        var written = await new ConfigurationChanges(source).SubmitAsync(
            new ConfigurationSubmission { Key = "Session:IdleTimeout", Value = value, Motive = "operational_adjustment", AuthorId = Host.AdministratorId }, CancellationToken.None);
        return written.ChangeId!.Value;
    }

    /// <summary>A change of the first administrator approved by the second and recorded by the core as never to be applied, for the reason given; the key of the change.</summary>
    public async Task<string> RefuseAsync(string reasonCode)
    {
        var id = await ProposeAsync("00:25:00");
        await using var source = NpgsqlDataSource.Create(Host.Database.AppConnectionString);
        var changes = new ConfigurationChanges(source);
        _ = await changes.DecideAsync(SecondAdministratorId, (await changes.SubmittedAsync(id, CancellationToken.None))!, approved: true, CancellationToken.None);
        var approved = new ApprovedChanges(source);
        var found = (await approved.PendingAsync(CancellationToken.None)).Single(change => change.Id == id);
        _ = await approved.RecordNotAppliedAsync(found, reasonCode, CancellationToken.None);
        return found.Key;
    }

    public ValueTask DisposeAsync() => Host.DisposeAsync();

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
}
