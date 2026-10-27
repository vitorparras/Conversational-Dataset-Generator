using Core.Host.Supervision;

namespace Core.Host.Tests.Supervision;

/// <summary>The service starts in the interpreter's isolated mode, so what the host inherited in PYTHONHOME and PYTHONPATH does not reach it.</summary>
public sealed class ServiceProcessLauncherTests : IDisposable
{
    private readonly SupervisionHarness harness = new();

    [Fact]
    public void InterpreterArgumentsStartWithTheIsolatedModeFlag()
    {
        var arguments = ServiceProcessLauncher.Arguments(5123);

        Assert.Equal(["-I", "-m", "language_service", "--port", "5123"], arguments);
    }

    [Fact]
    public async Task ServiceProcessRunsWithTheIsolatedModeFlagOnItsCommandLine()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var service = await harness.StartHealthyAsync(SupervisionHarness.NewSecret(), cancellationToken);

        var lines = await ProcessTree.TreeOfAsync(service.ProcessId, cancellationToken);

        Assert.Contains(lines, line => line.CommandLine.Contains(" -I -m language_service ", StringComparison.Ordinal));
    }

    public void Dispose() => harness.Dispose();
}
