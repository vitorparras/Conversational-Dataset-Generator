using System.Text.Json;
using System.Text.RegularExpressions;

namespace Core.Tests.Integration.Host;

/// <summary>Issue 51 on the real process: the versions answered, logged, and stable across runs.</summary>
public sealed partial class VersionsTests
{
    private static readonly string[] TreeStates =
        ["""{"state":"known","value":"true"}""", """{"state":"known","value":"false"}""", """{"state":"absent"}"""];

    private static readonly string[] AbsentAtThisStage = ["contract", "model", "schema"];

    [Fact]
    public async Task VersionsAnswerCoreCommitAndTreeWithTheRestAbsent()
    {
        var (versions, _) = await RunOnceAsync();

        Assert.Equal("known", versions.GetProperty("core").GetProperty("state").GetString());
        var commit = versions.GetProperty("commit");
        if (commit.GetProperty("state").GetString() == "known")
        {
            Assert.Matches(CommitPattern(), commit.GetProperty("value").GetString());
        }

        Assert.Contains(versions.GetProperty("dirtyTree").GetRawText(), TreeStates);
        foreach (var field in AbsentAtThisStage)
        {
            Assert.Equal("""{"state":"absent"}""", versions.GetProperty(field).GetRawText());
        }
    }

    [Fact]
    public async Task StartupLogCarriesTheSameVersionsAsTheEndpoint()
    {
        var (versions, output) = await RunOnceAsync();

        var described = string.Join(" ", versions.EnumerateObject().Select(field =>
            field.Name + "=" + (field.Value.GetProperty("state").GetString() == "known"
                ? field.Value.GetProperty("value").GetString()
                : "absent")));
        Assert.Contains("Versões em execução: " + described, output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TwoRunsOfTheSameArtifactAnswerTheSame()
    {
        var (first, _) = await RunOnceAsync();
        var (second, _) = await RunOnceAsync();

        Assert.Equal(first.GetRawText(), second.GetRawText());
    }

    [GeneratedRegex("^[0-9a-f]{40}$")]
    private static partial Regex CommitPattern();

    private static async Task<(JsonElement Versions, string Output)> RunOnceAsync()
    {
        using var installation = HostInstallation.Create();
        var port = HostInstallation.FreePort();
        installation.WriteSettings(HostInstallation.SettingsWithPort(port));
        await using var host = installation.Start();
        using var client = new HostClient(host, port);
        var versions = await client.GetWhenUpAsync("/versions");
        return (versions, host.Output);
    }
}
