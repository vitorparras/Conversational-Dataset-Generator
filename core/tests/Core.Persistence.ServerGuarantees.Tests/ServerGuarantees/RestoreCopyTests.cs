using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Core.Persistence.ServerGuarantees.Restore;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>The copy as the 005 describes it, read from a folder: the manifest that names it, the files it lists, and the batches one call of the load takes.</summary>
public sealed class RestoreCopyTests : IDisposable
{
    private const string Table = "public.domain";
    private readonly string folder = Path.Combine(Path.GetTempPath(), "zqx-copy-" + Guid.NewGuid().ToString("N"));

    public RestoreCopyTests()
    {
        _ = Directory.CreateDirectory(folder);
    }

    public void Dispose() => Directory.Delete(folder, recursive: true);

    [Fact]
    public void ManifestNamesTheCopyByItsDigestAndGivesTheVersionAndTheFiles()
    {
        var manifest = """{"format":1,"schemaVersion":20,"files":["public.domain.jsonl"]}""";
        File.WriteAllText(Path.Combine(folder, "manifest.json"), manifest);

        var copy = RestoreCopy.Open(folder);

        Assert.Equal((20, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(manifest)))), (copy.SchemaVersion, copy.Name));
        Assert.True(copy.Has(Table));
        Assert.False(copy.Has("public.person"));
    }

    [Theory]
    [InlineData("""{"format":2,"schemaVersion":20,"files":[]}""")]
    [InlineData("""{"format":1,"files":[]}""")]
    [InlineData("""{"format":1,"schemaVersion":"20","files":[]}""")]
    [InlineData("""{"format":1,"schemaVersion":20}""")]
    [InlineData("not json")]
    public void ManifestThatThisBuildDoesNotReadIsRefused(string manifest)
    {
        File.WriteAllText(Path.Combine(folder, "manifest.json"), manifest);

        _ = Assert.Throws<InvalidDataException>(() => RestoreCopy.Open(folder));
    }

    [Fact]
    public void FolderWithoutManifestIsRefusedAsAFileThatCannotBeRead()
        => _ = Assert.ThrowsAny<IOException>(() => RestoreCopy.Open(folder));

    [Fact]
    public void RowsComeInBatchesOfFiveHundredAsArraysWithoutTheEmptyLines()
    {
        File.WriteAllText(Path.Combine(folder, "manifest.json"), """{"format":1,"schemaVersion":20,"files":["public.domain.jsonl"]}""");
        File.WriteAllLines(Path.Combine(folder, Table + ".jsonl"), Enumerable.Range(1, 1203).SelectMany(id => new[] { $$"""{"id":{{id}}}""", string.Empty }));

        var batches = RestoreCopy.Open(folder).BatchesOf(Table).Select(batch => JsonNode.Parse(batch)!.AsArray()).ToList();

        Assert.Equal([500, 500, 203], batches.Select(batch => batch.Count));
        Assert.Equal(1203, (int)batches[2][202]!["id"]!);
    }

    [Fact]
    public void TableThatTheManifestDoesNotListHasNoBatches()
    {
        File.WriteAllText(Path.Combine(folder, "manifest.json"), """{"format":1,"schemaVersion":20,"files":[]}""");

        _ = Assert.Throws<InvalidDataException>(() => RestoreCopy.Open(folder).BatchesOf(Table).ToList());
    }

    [Fact]
    public void TableWithNoRowOffersNoBatch()
    {
        File.WriteAllText(Path.Combine(folder, "manifest.json"), """{"format":1,"schemaVersion":20,"files":["public.domain.jsonl"]}""");
        File.WriteAllText(Path.Combine(folder, Table + ".jsonl"), string.Empty);

        Assert.Empty(RestoreCopy.Open(folder).BatchesOf(Table));
    }

    [Theory]
    [InlineData(5, "abc", 5, "abc", true)]
    [InlineData(5, "abc", 5, "ABC", true)]
    [InlineData(5, "abc", 6, "abc", false)]
    [InlineData(5, "abc", 5, "abd", false)]
    public void TipMatchesTheSameEventAndNothingElse(long sequence, string digest, long otherSequence, string otherDigest, bool matches)
        => Assert.Equal(matches, new RestoreTip(sequence, digest).Matches(new RestoreTip(otherSequence, otherDigest)));

    [Fact]
    public void NoTipMatchesNoEvent()
        => Assert.False(new RestoreTip(1, "abc").Matches(null));
}
