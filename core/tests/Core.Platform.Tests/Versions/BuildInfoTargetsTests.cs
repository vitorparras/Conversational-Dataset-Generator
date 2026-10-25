using Core.Platform.Versions;

namespace Core.Platform.Tests.Versions;

/// <summary>
/// The build stamp asks git directly: a committed tree stamps its commit and a clean tree, an uncommitted change
/// stamps a dirty one, and a build where git is missing, or answers with an error, still succeeds and stamps absence.
/// </summary>
public sealed class BuildInfoTargetsTests
{
    private static readonly string SearchPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

    [Fact]
    public async Task CommittedTreeStampsItsVersionCommitAndACleanTree()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var workspace = await ProbeWorkspace.CommittedAsync(cancellationToken);

        var build = await workspace.BuildAsync(SearchPath, cancellationToken);

        Assert.Equal(
            new BuildInfo(VersionValue.Present("9.8.7"), VersionValue.Present(await workspace.HeadAsync(cancellationToken)), WorkingTree.Clean),
            build.Info);
    }

    [Fact]
    public async Task UncommittedChangeStampsADirtyTree()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var workspace = await ProbeWorkspace.CommittedAsync(cancellationToken);
        workspace.LeaveUncommittedChange();

        var build = await workspace.BuildAsync(SearchPath, cancellationToken);

        Assert.Equal(WorkingTree.Dirty, build.Info.WorkingTree);
    }

    [Fact]
    public async Task BuildWithAnEmptySearchPathStampsTheVersionAndNothingElse()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var workspace = await ProbeWorkspace.CommittedAsync(cancellationToken);

        var build = await workspace.BuildAsync(string.Empty, cancellationToken);

        Assert.Equal(new BuildInfo(VersionValue.Present("9.8.7"), VersionValue.Absent, WorkingTree.Unknown), build.Info);
        Assert.Equal(["BuildInfo.Version"], build.StampedKeys);
    }

    [Fact]
    public async Task BuildOutsideAnyRepositoryStampsTheVersionAndNothingElse()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var workspace = ProbeWorkspace.OutsideAnyRepository();

        var build = await workspace.BuildAsync(SearchPath, cancellationToken);

        Assert.Equal(new BuildInfo(VersionValue.Present("9.8.7"), VersionValue.Absent, WorkingTree.Unknown), build.Info);
        Assert.Equal(["BuildInfo.Version"], build.StampedKeys);
    }
}
