using System.Security.Cryptography;
using System.Text.Json;
using Core.Ingestion.Entrance;
using Core.Ingestion.Reading;

namespace Core.Ingestion.Tests.Entrance;

/// <summary>
/// The corpus of hostile files in <c>core/tests/fixtures/ingestion-hostile</c> (issue 75, criterion 3): each file, with the name it came with,
/// goes through the gate and has the outcome the manifest says, with the digest of its bytes and a reason that names no word of the file or of the name.
/// </summary>
public sealed class HostileCorpusTests
{
    private const long DefaultLimit = 1_048_576;

    private static readonly string Folder = Path.Combine(Root(), "tests", "fixtures", "ingestion-hostile");

    public static TheoryData<int> Cases()
    {
        var data = new TheoryData<int>();
        for (var at = 0; at < Manifest().Count; at++)
        {
            data.Add(at);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task EachHostileFileHasTheOutcomeTheManifestSaysWithItsDigestAndNoWordOfTheFileInTheReason(int at)
    {
        var entry = Manifest()[at];
        var bytes = await File.ReadAllBytesAsync(Path.Combine(Folder, entry.File), TestContext.Current.CancellationToken);
        var gate = new SubmissionGate(SourceReaders.Default(), entry.Limit ?? DefaultLimit);

        var result = await gate.ReceiveAsync(new Submission(new MemoryStream(bytes), "cdg-conversa/1", entry.Name), TestContext.Current.CancellationToken);

        Assert.Equal(entry.Expect, Describe(result));
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes.AsSpan(0, (int)result.BytesRead))), result.Digest);
        Assert.DoesNotContain("ZQX", result.Reason ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(entry.File, result.Reason ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryFileOfTheCorpusIsInTheManifestAndEveryEntryHasItsFile()
    {
        var files = Directory.GetFiles(Folder).Select(path => Path.GetFileName(path) ?? string.Empty).Where(name => name != "manifest.json").Order(StringComparer.Ordinal).ToList();
        var listed = Manifest().Select(entry => entry.File).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(files, listed);
    }

    [Fact]
    public void TheCorpusHoldsTheHostileCasesThePlanNames()
    {
        var names = Manifest().Select(entry => entry.File + "|" + entry.Name).ToList();

        Assert.Multiple(
            () => Assert.Contains(names, name => name.StartsWith("deep-json.json", StringComparison.Ordinal)),
            () => Assert.Contains(names, name => name.StartsWith("huge-string.json", StringComparison.Ordinal)),
            () => Assert.Contains(names, name => name.StartsWith("duplicate-key.json", StringComparison.Ordinal)),
            () => Assert.Contains(names, name => name.StartsWith("bom.json", StringComparison.Ordinal)),
            () => Assert.Contains(names, name => name.StartsWith("utf16-with-bom.json", StringComparison.Ordinal)),
            () => Assert.Contains(names, name => name.StartsWith("invalid-bytes-inside.json", StringComparison.Ordinal)),
            () => Assert.Contains(names, name => name.Contains("../", StringComparison.Ordinal)),
            () => Assert.Contains(names, name => name.Any(char.IsControl)));
    }

    private static string Describe(SubmissionResult result)
    {
        if (result.Refusal is not { } refusal)
        {
            return "read";
        }

        return refusal == SubmissionRefusal.Unreadable ? $"Unreadable:{result.Cause}" : refusal.ToString();
    }

    private static List<CorpusEntry> Manifest()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Folder, "manifest.json")));
        return [.. document.RootElement.EnumerateArray().Select(line => new CorpusEntry(
            line.GetProperty("file").GetString()!,
            line.GetProperty("name").GetString(),
            line.GetProperty("expect").GetString()!,
            line.TryGetProperty("limit", out var limit) ? limit.GetInt64() : null))];
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Core.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Core.slnx was not found above the test output.");
    }
}
