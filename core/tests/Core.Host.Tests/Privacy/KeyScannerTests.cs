using Core.Tests.Support.Custody;

namespace Core.Host.Tests.Privacy;

/// <summary>The search that proves the key is nowhere it must not be finds it where it is, so that an empty answer means something.</summary>
public sealed class KeyScannerTests : IDisposable
{
    private static readonly byte[] Key = CustodyFiles.SyntheticKey();

    private readonly TemporaryDirectory directory = new();

    public void Dispose() => directory.Dispose();

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void FileHoldingASpellingOfTheKeyInsideOtherBytesIsFound(int spelling)
    {
        var file = Path.Combine(directory.Path, "holding");
        File.WriteAllBytes(file, [.. "antes "u8.ToArray(), .. KeySpellings.Of(Key)[spelling], .. " depois"u8.ToArray()]);

        var found = KeySpellings.FilesHolding([file], Key);

        Assert.Equal([file], found);
    }

    [Fact]
    public void FileWithoutTheKeyIsNotFoundEvenWhenItHoldsAHalfOfIt()
    {
        var file = Path.Combine(directory.Path, "half");
        File.WriteAllBytes(file, Key[..16]);

        var found = KeySpellings.FilesHolding([file], Key);

        Assert.Empty(found);
    }

    [Fact]
    public void FileThatIsGoneByTheTimeItIsReadHoldsNothing()
    {
        var found = KeySpellings.FilesHolding([Path.Combine(directory.Path, "gone")], Key);

        Assert.Empty(found);
    }

    [Fact]
    public void ListingOfTheRepositoryHoldsTheSourcesOfTheHostAndOfThisTest()
    {
        var files = RepositoryFiles.All();

        Assert.Contains(files, file => file.EndsWith("core/src/Core.Host/CompositionRoot.cs", StringComparison.Ordinal));
        Assert.Contains(files, file => file.EndsWith("core/tests/Core.Host.Tests/Privacy/KeyScannerTests.cs", StringComparison.Ordinal));
    }
}
