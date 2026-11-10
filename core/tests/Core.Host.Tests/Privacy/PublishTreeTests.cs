namespace Core.Host.Tests.Privacy;

/// <summary>The build tree of the publish is the same for a working copy, another for another, and is held by one taker at a time.</summary>
public sealed class PublishTreeTests
{
    [Fact]
    public async Task OneWorkingCopyGetsTheSameFolderEveryTimeAndAnotherGetsItsOwn()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var root = $"/work/{Guid.NewGuid():N}";
        string first;
        using (var tree = await PublishTree.TakeAsync(root, cancellationToken))
        {
            first = tree.Path;
        }

        using var again = await PublishTree.TakeAsync(root, cancellationToken);
        using var other = await PublishTree.TakeAsync($"{root}-other", cancellationToken);

        Assert.Equal(first, again.Path);
        Assert.NotEqual(first, other.Path);
        Assert.StartsWith(Path.Combine(Path.GetTempPath(), "core-host-publish"), first, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SecondTakerWaitsUntilTheFirstLetsGo()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var root = $"/work/{Guid.NewGuid():N}";
        using var first = await PublishTree.TakeAsync(root, cancellationToken);

        var second = Task.Run(async () =>
        {
            using var taken = await PublishTree.TakeAsync(root, cancellationToken);
            return taken.Path;
        }, cancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        var waitedWhileHeld = !second.IsCompleted;
        first.Dispose();
        var takenPath = await second.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

        Assert.True(waitedWhileHeld);
        Assert.Equal(first.Path, takenPath);
    }
}
