using Core.Boundary.Http;
using Core.Host.Endpoints;
using Core.Host.Supervision;
using Core.Platform;
using Core.Platform.Startup;
using Core.Platform.Versions;

namespace Core.Host.Tests;

/// <summary>
/// What the routes of status and of versions and the screen of the state of the system share (issue 604): one reading of what is alive, ready and in force, so the screen says what the route says.
/// </summary>
public sealed class SystemStateTests
{
    private static readonly DateTimeOffset CheckedAt = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly CheckResult Passed = CheckResult.Passed("Sample:File", "recurso presente", "ZQX-MARKER-7731.bin");
    private static readonly CheckResult Failed = CheckResult.Failed("Sample:File", "recurso ausente", "ZQX-MARKER-7731.bin");
    private static readonly VersionReport Core = new(new BuildInfo(VersionValue.Present("9.8.7"), VersionValue.Present("0123456789abcdef"), WorkingTree.Clean));

    [Fact]
    public void WithTheFakeAndEveryCheckPassedTheProcessIsAliveAndReadyAndTheServiceIsNotSaid()
    {
        var status = SystemState.Status(Sources(Passed), RecognizerKind.Fake);

        Assert.Equal((true, true, RecognizerKind.Fake), (status.Alive, status.Ready, status.Recognizer));
        Assert.Null(status.LanguageService);
        Assert.Equal(CheckedAt, status.CheckedAt);
        Assert.Equal(Passed, Assert.Single(status.Checks));
    }

    [Fact]
    public void AFailedCheckLeavesTheProcessAliveButNotReadyAndSaysTheCheck()
    {
        var status = SystemState.Status(Sources(Failed), RecognizerKind.Fake);

        Assert.Equal((true, false), (status.Alive, status.Ready));
        Assert.Equal(Failed, Assert.Single(status.Checks));
    }

    [Fact]
    public void WithTheHttpRecognizerReadinessWaitsForTheServiceAndTheStateOfItIsSaid()
    {
        var watch = new ServiceProcessWatch();

        var status = SystemState.Status(Sources(watch, Passed), RecognizerKind.Http);

        Assert.Equal(watch.State, status.LanguageService);
        Assert.Equal(watch.State == ServiceState.Available, status.Ready);
    }

    [Fact]
    public async Task TheVersionsAreTheOnesOfTheProcessWithTheThreeOfTheServiceAskedOfItOnTheCall()
    {
        var declared = new ServiceVersions(VersionValue.Present("7"), VersionValue.Present("0.4.2"), VersionValue.Present("pt-model-3.2"));

        var versions = await SystemState.VersionsAsync(Core, new Reader(declared), TestContext.Current.CancellationToken);

        Assert.Equal(
            [VersionValue.Present("9.8.7"), VersionValue.Present("7"), VersionValue.Present("0.4.2"), VersionValue.Present("pt-model-3.2")],
            [versions.CoreVersion, versions.ContractVersion, versions.LanguageServiceVersion, versions.ModelVersion]);
    }

    [Fact]
    public async Task AServiceThatGaveNothingLeavesItsThreeAbsent()
    {
        var versions = await SystemState.VersionsAsync(Core, new Reader(ServiceVersions.Absent), TestContext.Current.CancellationToken);

        Assert.Equal([VersionValue.Absent, VersionValue.Absent, VersionValue.Absent], [versions.ContractVersion, versions.LanguageServiceVersion, versions.ModelVersion]);
    }

    private static StatusSources Sources(CheckResult check) => Sources(new ServiceProcessWatch(), check);

    private static StatusSources Sources(ServiceProcessWatch watch, CheckResult check) =>
        new(new StartupReport([check], CheckedAt), watch, new Installation(InstallationKind.Real), new AuditComparisonState(), new SealWarningState());

    private sealed class Reader(ServiceVersions answer) : IServiceVersionReader
    {
        public Task<ServiceVersions> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(answer);
    }
}
