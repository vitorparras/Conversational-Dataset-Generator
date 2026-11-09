using System.Text.Json.Nodes;
using Core.Host.Settings;

namespace Core.Host.Tests.Settings;

/// <summary>
/// The write of the file of the configuration (#634, part 4; decision 0053, CA-4): one key changes and no other, the version before is kept under a name that carries the moment, the new file
/// takes the place of the old in one step, and a file the applier cannot read as the configuration is left exactly as it was.
/// </summary>
public sealed class ConfigurationFileTests : IDisposable
{
    private const string Key = "Session:IdleTimeout";

    private readonly TemporaryDirectory folder = new();
    private readonly ManualClock clock = new();

    [Fact]
    public void TheKeyChangesAndEveryOtherKeyStaysAsItWas()
    {
        var path = Written("""{ "Host": { "Urls": "https://127.0.0.1:5000" }, "Session": { "IdleTimeout": "00:30:00", "AbsoluteTimeout": "08:00:00" } }""");

        var changed = new ConfigurationFile(path, clock).TryChange(Key, "00:20:00");

        var after = Read(path);
        Assert.True(changed);
        Assert.Equal(("00:20:00", "08:00:00", "https://127.0.0.1:5000"), (Text(after, "Session", "IdleTimeout"), Text(after, "Session", "AbsoluteTimeout"), Text(after, "Host", "Urls")));
    }

    [Fact]
    public void TheVersionBeforeIsKeptWithTheMomentInItsNameAndItsContentAsItWas()
    {
        const string Before = """{ "Session": { "IdleTimeout": "00:30:00" } }""";
        var path = Written(Before);

        _ = new ConfigurationFile(path, clock).TryChange(Key, "00:20:00");

        var kept = Assert.Single(Directory.GetFiles(folder.Path, "*.previous"));
        Assert.EndsWith($".{clock.GetUtcNow():yyyyMMdd'T'HHmmss'Z'}.previous", kept, StringComparison.Ordinal);
        Assert.Equal(Before, File.ReadAllText(kept));
    }

    [Fact]
    public void AKeyWhoseSectionIsNotInTheFileCreatesTheSectionAndKeepsNothingToKeep()
    {
        var path = Written("""{ "Host": { "Urls": "https://127.0.0.1:5000" } }""");

        var changed = new ConfigurationFile(path, clock).TryChange(Key, "00:20:00");

        Assert.True(changed);
        Assert.Equal("00:20:00", Text(Read(path), "Session", "IdleTimeout"));
    }

    [Fact]
    public void AFileThatIsNotJsonIsLeftExactlyAsItWasAndNothingIsKept()
    {
        const string Broken = "{ isto não é JSON";
        var path = Written(Broken);

        var changed = new ConfigurationFile(path, clock).TryChange(Key, "00:20:00");

        Assert.False(changed);
        Assert.Equal((Broken, 1), (File.ReadAllText(path), Directory.GetFiles(folder.Path).Length));
    }

    [Fact]
    public void ASectionThatIsNotAnObjectIsLeftExactlyAsItWas()
    {
        const string Odd = """{ "Session": "texto" }""";
        var path = Written(Odd);

        var changed = new ConfigurationFile(path, clock).TryChange(Key, "00:20:00");

        Assert.False(changed);
        Assert.Equal(Odd, File.ReadAllText(path));
    }

    [Fact]
    public void AKeyThatIsNotSectionAndNameChangesNothing()
    {
        var path = Written("{}");

        Assert.False(new ConfigurationFile(path, clock).TryChange("SemSecao", "x"));
    }

    public void Dispose() => folder.Dispose();

    private static JsonObject Read(string path) => (JsonObject)JsonNode.Parse(File.ReadAllText(path))!;

    private static string Text(JsonObject root, string section, string name) => (string)root[section]![name]!;

    private string Written(string content)
    {
        var path = Path.Combine(folder.Path, "appsettings.json");
        File.WriteAllText(path, content);
        return path;
    }
}
