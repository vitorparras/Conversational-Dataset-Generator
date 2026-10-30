using System.Net;
using System.Net.Sockets;
using Core.Host.Supervision;

namespace Core.Host.Tests.Supervision;

/// <summary>The service starts in the interpreter's isolated mode, so what the host inherited in PYTHONHOME and PYTHONPATH does not reach it, and on a port it opens itself and tells.</summary>
public sealed class ServiceProcessLauncherTests : IDisposable
{
    private readonly SupervisionHarness harness = new();

    [Fact]
    public void InterpreterArgumentsStartWithTheIsolatedModeFlagAndLeaveThePortToTheService()
    {
        var arguments = ServiceProcessLauncher.Arguments();

        Assert.Equal(["-I", "-m", "language_service", "--port", "0"], arguments);
    }

    [Trait("Category", "RealService")]
    [Fact]
    public async Task ServiceProcessRunsWithTheIsolatedModeFlagOnItsCommandLine()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var service = await harness.StartHealthyAsync(SupervisionHarness.NewSecret(), cancellationToken);

        var lines = await ProcessTree.TreeOfAsync(service.ProcessId, cancellationToken);

        Assert.Contains(lines, line => line.CommandLine.Contains(" -I -m language_service ", StringComparison.Ordinal));
    }

    [Trait("Category", "RealService")]
    [Fact]
    public async Task ServiceTellsThePortItHoldsAndNobodyElseCanTakeIt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var service = await harness.StartHealthyAsync(SupervisionHarness.NewSecret(), cancellationToken);

        using var rival = new TcpListener(IPAddress.Loopback, service.Port) { ExclusiveAddressUse = true };

        _ = Assert.Throws<SocketException>(rival.Start);
    }

    public void Dispose() => harness.Dispose();
}
