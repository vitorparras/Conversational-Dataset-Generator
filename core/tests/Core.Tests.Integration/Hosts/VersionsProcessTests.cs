using Core.Persistence.Tests.Fixtures;
using System.Text.Json;
using Core.Host.Tests;

namespace Core.Tests.Integration.Hosts;

/// <summary>The built host, entered, answers under which versions it runs: its build as git saw it, absence for what the fake cannot obtain, and the name of its installation.</summary>
[Trait("Category", "Database")]
public sealed class VersionsProcessTests(DefaultHostFixture fixture) : IClassFixture<DefaultHostFixture>
{
    private static readonly string[] Fields =
        ["coreVersion", "commit", "workingTree", "installation", "contractVersion", "languageServiceVersion", "modelVersion", "schemaVersion"];

    private static readonly string[] ServiceFields = ["contractVersion", "languageServiceVersion", "modelVersion"];

    [DatabaseFact]
    public async Task CoreVersionIsTheDeclaredOneAndNotTheSdkDefault()
    {
        using var versions = await VersionsAsync(fixture.Host);

        Assert.Equal("0.1.0", versions.RootElement.GetProperty("coreVersion").GetString());
    }

    [DatabaseFact]
    public async Task VersionsNameTheCommitTheHostWasBuiltFrom()
    {
        using var versions = await VersionsAsync(fixture.Host);

        Assert.Equal(WorkingCopy.Head(), versions.RootElement.GetProperty("commit").GetString());
    }

    [DatabaseFact]
    public async Task VersionsSayWhetherTheTreeHadUncommittedChanges()
    {
        using var versions = await VersionsAsync(fixture.Host);

        Assert.Equal(WorkingCopy.TreeState(), versions.RootElement.GetProperty("workingTree").GetString());
    }

    [DatabaseFact]
    public async Task VersionsAnswerEveryFieldAndNoOther()
    {
        using var versions = await VersionsAsync(fixture.Host);

        Assert.Equal(Fields, versions.RootElement.EnumerateObject().Select(field => field.Name));
    }

    [DatabaseFact]
    public async Task VersionsTheFakeRecognizerHostCannotObtainAreNullAndNotEmpty()
    {
        using var versions = await VersionsAsync(fixture.Host);

        Assert.Equal(
            [JsonValueKind.Null, JsonValueKind.Null, JsonValueKind.Null],
            ServiceFields.Select(field => versions.RootElement.GetProperty(field).ValueKind));
    }

    [DatabaseFact]
    public async Task SchemaVersionOfAHostOverAMigratedDatabaseIsTheOneTheMigrationsLeft()
    {
        using var versions = await VersionsAsync(fixture.Host);

        Assert.Equal(JsonValueKind.String, versions.RootElement.GetProperty("schemaVersion").ValueKind);
        Assert.NotEmpty(versions.RootElement.GetProperty("schemaVersion").GetString()!);
    }

    [DatabaseFact]
    public async Task TwoStartsOfTheSameBuildAnswerTheSameVersions()
    {
        var first = await VersionsTextAsync(fixture.Host);
        await using var other = await AuthenticatedHost.StartAsync(new HostSetup(), TestContext.Current.CancellationToken);

        var second = await VersionsTextAsync(other);

        Assert.Equal(first, second);
    }

    [DatabaseFact]
    public async Task ProcessOfADemonstrationAnswersTheMarkedNameOnTheVersions()
    {
        using var versions = await VersionsAsync(fixture.Host);

        Assert.Contains("(DEMONSTRAÇÃO)", versions.RootElement.GetProperty("installation").GetProperty("name").GetString(), StringComparison.Ordinal);
    }

    private static async Task<JsonDocument> VersionsAsync(AuthenticatedHost host) => JsonDocument.Parse(await VersionsTextAsync(host));

    private static async Task<string> VersionsTextAsync(AuthenticatedHost host)
    {
        using var response = await host.GetAsync("/versions", TestContext.Current.CancellationToken);
        _ = response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }
}
