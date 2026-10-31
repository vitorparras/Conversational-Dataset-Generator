using Core.Tests.Support.Custody;

namespace Core.Host.Tests.Privacy;

/// <summary>After the host has started with a key of the custody files, no spelling of that key is in the repository nor in what the publish writes.</summary>
public sealed class KeyAtRestTests
{
    private static readonly byte[] Key = CustodyFiles.SyntheticKey();

    [Fact]
    public async Task RepositoryHoldsNoSpellingOfTheKeyTheHostStartedWith()
    {
        var status = await KeyedHost.RunAsync(TestContext.Current.CancellationToken);

        var found = KeySpellings.FilesHolding(RepositoryFiles.All(), Key);

        Assert.Equal("Passed", KeyedHost.StatusOf(status, "MasterKey"));
        Assert.Empty(found);
    }

    [Fact]
    public async Task PublishWritesNoSpellingOfTheKeyTheHostStartedWith()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var folder = new TemporaryDirectory();
        var status = await KeyedHost.RunAsync(cancellationToken);

        var published = await PublishedHost.IntoAsync(folder.Path, cancellationToken);
        var found = KeySpellings.FilesHolding(published, Key);

        Assert.Equal("Passed", KeyedHost.StatusOf(status, "MasterKey"));
        Assert.Contains(published, file => file.EndsWith("Core.Host.dll", StringComparison.Ordinal));
        Assert.Empty(found);
    }
}
