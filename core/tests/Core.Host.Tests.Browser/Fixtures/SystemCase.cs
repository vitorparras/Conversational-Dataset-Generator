using System.Diagnostics.CodeAnalysis;
using Core.Boundary.Fake;
using Core.Boundary.Http;
using Core.Host.Endpoints;
using Core.Host.Supervision;
using Core.Platform;
using Core.Platform.Configuration;
using Core.Platform.Configuration.Sources;
using Core.Platform.Startup;
using Core.Platform.Versions;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>
/// The host of the screens of the configuration in force and of the state of the system (issues 603 and 604) on a real Kestrel, behind the real session gate and the real screen
/// gate, with the real routes of both screens, over a migrated database of its own that holds three people made up for the run: the administration, the audit and an annotation. The
/// configuration is the one of the host with the keys of the safekeeping copy, one of them secret, written as an argument with a value made up for the run, <see cref="Secret"/>,
/// which no page may show. The startup report is what the test sets. The channel is told to be encrypted, which the loopback of the test cannot be.
/// </summary>
internal sealed class SystemCase : IAsyncDisposable
{
    public const string DeclaredDate = "2026-10-01";
    public const string PassedCheck = "Chave mestra";
    public const string FailedCheck = "Servidor de banco";
    public const string FailedMessage = "a versão do servidor é menor que a exigida";
    public const string FailedObserved = "PostgreSQL 14.2";

    private readonly ReportKeeper keeper;

    private SystemCase(ScreenHost host, string secret, EffectiveConfiguration configuration, ReportKeeper keeper)
    {
        Host = host;
        Secret = secret;
        Configuration = configuration;
        this.keeper = keeper;
    }

    /// <summary>The host of the case.</summary>
    public ScreenHost Host { get; }

    /// <summary>The value of the secret key of the run, which no page may show anywhere.</summary>
    public string Secret { get; }

    /// <summary>The configuration in force the screen shows, which the test compares with the page.</summary>
    public EffectiveConfiguration Configuration { get; }

    /// <summary>The startup report the next request of the state reads: one check that passed and one that failed, until a test sets another.</summary>
    public StartupReport Report
    {
        get => keeper.Current;
        set => keeper.Current = value;
    }

    [SuppressMessage("Reliability", "CA2000", Justification = "The host goes to the case that is returned, which disposes it.")]
    public static async Task<SystemCase> StartAsync(CancellationToken cancellationToken)
    {
        var secret = $"zqx-segredo-{Guid.NewGuid():N}";
        var configuration = ClosedConfiguration.Load(
            new ConfigurationSchema([HostConfiguration.Section, SafekeepingConfiguration.Section]),
            new StrictArgumentsSource([$"--{SafekeepingConfiguration.HolderPath}={secret}", $"--{SafekeepingConfiguration.LastRecoveryTestPath}={DeclaredDate}"])).View;
        var keeper = new ReportKeeper();
        var host = await ScreenHost.StartAsync(
            new ScreenParts
            {
                Configuration = configuration,
                Services = (services, _) => services.AddTransient(_ => keeper.Current)
                    .AddSingleton(new ServiceProcessWatch())
                    .AddSingleton(new Installation(InstallationKind.Demonstration))
                    .AddSingleton<AuditComparisonState>()
                    .AddSingleton<SealWarningState>()
                    .AddSingleton(new VersionReport(new BuildInfo(VersionValue.Present("9.8.7"), VersionValue.Present("0123456789abcdef0123456789abcdef01234567"), WorkingTree.Clean))
                        .WithSchemaVersion(VersionValue.Present("0042")))
                    .AddSingleton<IServiceVersionReader, NoServiceVersionReader>(),
                Screens = routes => SystemScreensEndpoint.Map(routes, RecognizerKind.Fake),
            },
            cancellationToken);
        return new SystemCase(host, secret, configuration, keeper);
    }

    public ValueTask DisposeAsync() => Host.DisposeAsync();

    /// <summary>The startup report the host reads at each request, which a test can replace.</summary>
    private sealed class ReportKeeper
    {
        public StartupReport Current { get; set; } = new(
            [
                CheckResult.Passed(PassedCheck, "a chave foi lida e confere", "arquivo de chaves do servidor"),
                CheckResult.Failed(FailedCheck, FailedMessage, "servidor de banco da instalação") with { Observed = FailedObserved },
            ],
            new DateTimeOffset(2026, 11, 6, 9, 30, 0, TimeSpan.Zero));
    }
}
