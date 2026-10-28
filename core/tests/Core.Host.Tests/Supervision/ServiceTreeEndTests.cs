using Core.Host.Supervision;

namespace Core.Host.Tests.Supervision;

/// <summary>
/// Nothing of the language service outlives its end: not the process the core launched, and not what that process started,
/// whether the service is killed from outside or the core ends it (issue #369). The interpreter of a venv is a launcher with
/// a child on Windows; the standalone one, which is the form of the service folder (decision 0041), has none. The venv runs
/// the real service; the standalone interpreter runs a stand-in, because the folder with its model is built apart from the tests.
/// </summary>
public sealed class ServiceTreeEndTests : IDisposable
{
    private readonly SupervisionHarness harness = new();

    [Fact]
    public async Task KilledServiceOfTheDevelopmentVenvLeavesNoProcessOfItsTree()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var supervisor = await harness.StartedSupervisorAsync(new ServiceProcessWatch(), cancellationToken);
        var tree = await TreeOfAsync(supervisor.ProcessId!.Value, cancellationToken);
        using var survivors = new Survivors(tree);

        StandInService.Kill(supervisor.ProcessId.Value);

        Assert.True(await EndedAsync(tree, cancellationToken), "a process of the service tree outlived the service killed from outside");
    }

    [Fact]
    public async Task StoppedSupervisorOfTheDevelopmentVenvLeavesNoProcessOfTheServiceTree()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var supervisor = await harness.StartedSupervisorAsync(new ServiceProcessWatch(), cancellationToken);
        var tree = await TreeOfAsync(supervisor.ProcessId!.Value, cancellationToken);
        using var survivors = new Survivors(tree);

        supervisor.Dispose();

        Assert.True(await EndedAsync(tree, cancellationToken), "a process of the service tree outlived the end the core asked for");
    }

    [Fact]
    public async Task KilledStandaloneServiceLeavesNoProcessOfItsTree()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var service = StandInService.Start(LanguageServiceInterpreter.Standalone);
        var tree = await TreeOfAsync(service.ProcessId, cancellationToken);
        using var survivors = new Survivors(tree);

        StandInService.Kill(service.ProcessId);

        Assert.True(await EndedAsync(tree, cancellationToken), "a process of the service tree outlived the service killed from outside");
    }

    [Fact]
    public async Task StoppedStandaloneServiceLeavesNoProcessOfItsTree()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var service = StandInService.Start(LanguageServiceInterpreter.Standalone);
        var tree = await TreeOfAsync(service.ProcessId, cancellationToken);
        using var survivors = new Survivors(tree);

        service.Dispose();

        Assert.True(await EndedAsync(tree, cancellationToken), "a process of the service tree outlived the end the core asked for");
    }

    public void Dispose() => harness.Dispose();

    /// <summary>The process and everything below it, read while it is alive: once the parent is gone a child no longer shows under it.</summary>
    private static async Task<IReadOnlyList<int>> TreeOfAsync(int rootId, CancellationToken cancellationToken)
    {
        var lines = await ProcessTree.TreeOfAsync(rootId, cancellationToken);
        var ids = lines.Select(line => line.Id).ToList();
        Assert.Contains(rootId, ids);
        return ids;
    }

    private static Task<bool> EndedAsync(IEnumerable<int> ids, CancellationToken cancellationToken) =>
        Poll.UntilAsync(() => Task.FromResult(!ids.Any(ProcessTree.IsRunning)), Poll.LossLimit, cancellationToken);
}
