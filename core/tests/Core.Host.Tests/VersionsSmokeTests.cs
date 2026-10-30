using System.Text.Json;

namespace Core.Host.Tests;

/// <summary>The built host answers under which versions it runs: its own build as git saw it, and absence for the rest.</summary>
public sealed class VersionsSmokeTests
{
    private static readonly string[] Fields =
        ["coreVersion", "commit", "workingTree", "installation", "contractVersion", "languageServiceVersion", "modelVersion", "schemaVersion"];

    [Fact]
    public async Task CoreVersionIsTheDeclaredOneAndNotTheSdkDefault()
    {
        using var versions = await VersionsAsync(TestContext.Current.CancellationToken);

        Assert.Equal("0.1.0", versions.RootElement.GetProperty("coreVersion").GetString());
    }

    [Fact]
    public async Task VersionsNameTheCommitTheHostWasBuiltFrom()
    {
        using var versions = await VersionsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(WorkingCopy.Head(), versions.RootElement.GetProperty("commit").GetString());
    }

    [Fact]
    public async Task VersionsSayWhetherTheTreeHadUncommittedChanges()
    {
        using var versions = await VersionsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(WorkingCopy.TreeState(), versions.RootElement.GetProperty("workingTree").GetString());
    }

    [Fact]
    public async Task VersionsAnswerEveryFieldAndNoOther()
    {
        using var versions = await VersionsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(Fields, versions.RootElement.EnumerateObject().Select(field => field.Name));
    }

    [Fact]
    public async Task VersionsTheFakeRecognizerHostCannotObtainAreNullAndNotEmpty()
    {
        using var versions = await VersionsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [JsonValueKind.Null, JsonValueKind.Null, JsonValueKind.Null],
            VersionsHost.ServiceKinds(versions));
    }

    [Fact]
    public async Task SchemaVersionWithoutADatabaseIsNullAndNotEmptyNorZero()
    {
        using var versions = await VersionsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(JsonValueKind.Null, versions.RootElement.GetProperty("schemaVersion").ValueKind);
    }

    [Fact]
    public async Task TwoStartsOfTheSameBuildAnswerTheSameVersions()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var first = await VersionsTextAsync(cancellationToken);

        var second = await VersionsTextAsync(cancellationToken);

        Assert.Equal(first, second);
    }

    private static async Task<JsonDocument> VersionsAsync(CancellationToken cancellationToken) =>
        JsonDocument.Parse(await VersionsTextAsync(cancellationToken));

    /// <summary>Starts the built host, reads its versions and stops it, so each call is a start of its own.</summary>
    private static async Task<string> VersionsTextAsync(CancellationToken cancellationToken)
    {
        using var host = await HostProcess.StartAsync(cancellationToken);
        using var response = await host.GetAsync("/versions", cancellationToken);
        _ = response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
