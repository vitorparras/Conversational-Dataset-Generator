using Core.Platform.Startup;

namespace Core.Host.Tests.Supervision;

/// <summary>The real host with the real service: the host does not start without it, and the service does not outlive a host that dies.</summary>
public sealed class SupervisedHostTests
{
    private static readonly TimeSpan ServiceExitLimit = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    [Fact]
    public async Task ServiceEndsWithinFiveSecondsWhenTheHostIsKilledWithoutWarning()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var address = HostProcess.FreeAddress();
        var installation = HostInstallation.Built(address) with { UsesRealService = true };
        using var host = await HostProcess.StartAsync(installation, address, HostProcess.StartupTimeout, cancellationToken);
        var tree = await ProcessTree.TreeOfAsync(host.Id, cancellationToken);
        var service = tree.Select(line => line.Id).Where(id => id != host.Id).ToList();
        Assert.NotEmpty(service);
        using var survivors = new Survivors(service);

        host.KillHostOnly();
        var ended = await EndedWithinAsync(service, ServiceExitLimit, cancellationToken);

        Assert.True(ended, "the service outlived a host killed without warning");
    }

    [Fact]
    public async Task HostWithoutTheInterpreterKeyEndsWithTheRefusalCodeAndNamesTheKey()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var copy = new HostCopy(/*lang=json*/ "{}");
        var installation = copy.Installation() with { ProvidesLanguageService = false };

        var exit = await HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, cancellationToken);

        Assert.Equal((int)ExitCode.ConfigurationRefused, exit.Code);
        Assert.Contains("'LanguageService:Executable'", exit.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HostWhoseInterpreterDoesNotExistEndsWithTheCheckCodeAndNamesWhereItComesFrom()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var copy = new HostCopy(/*lang=json*/ "{}");
        var installation = copy.Installation("--LanguageService:Executable=zqx-missing-7731") with { ProvidesLanguageService = false };

        var exit = await HostProcess.RunToExitAsync(installation, HostProcess.StartupTimeout, cancellationToken);

        Assert.Equal((int)ExitCode.StartupCheckFailed, exit.Code);
        Assert.Contains("LanguageService", exit.Error, StringComparison.Ordinal);
        Assert.Contains("origem esperada: LanguageService:Executable", exit.Error, StringComparison.Ordinal);
    }

    private static async Task<bool> EndedWithinAsync(IReadOnlyList<int> ids, TimeSpan limit, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + limit;
        while (DateTimeOffset.UtcNow < deadline && ids.Any(ProcessTree.IsRunning))
        {
            await Task.Delay(PollInterval, cancellationToken);
        }

        return !ids.Any(ProcessTree.IsRunning);
    }
}
