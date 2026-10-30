using System.Text.Json;
using Core.Boundary.Fake;
using Core.Boundary.Http;
using Core.Platform.Versions;

namespace Core.Host.Tests;

/// <summary>The versions route asks the service for its versions on each request and answers them beside the fields that do not change.</summary>
public sealed class VersionsRouteTests
{
    private static readonly string[] Fields =
        ["coreVersion", "commit", "workingTree", "installation", "contractVersion", "languageServiceVersion", "modelVersion", "schemaVersion"];

    [Fact]
    public async Task VersionsTheServiceGaveAreAnsweredEachInItsOwnField()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await VersionsHost.StartAsync(new ScriptedReader(Declared("7", "0.4.2", "pt-model-3.2")), cancellationToken);

        using var versions = await host.VersionsAsync(cancellationToken);

        Assert.Equal(
            ["7", "0.4.2", "pt-model-3.2"],
            VersionsHost.ServiceTexts(versions));
    }

    [Fact]
    public async Task ContractTheServiceDeclaresIsAnsweredAndNotTheOneTheCoreSpeaks()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await VersionsHost.StartAsync(new ScriptedReader(Declared("99", "0.4.2", "pt-model-3.2")), cancellationToken);

        using var versions = await host.VersionsAsync(cancellationToken);

        Assert.Equal("99", versions.RootElement.GetProperty("contractVersion").GetString());
    }

    [Fact]
    public async Task VersionsTheServiceDidNotGiveAreNullAndNotEmpty()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await VersionsHost.StartAsync(new ScriptedReader(ServiceVersions.Absent), cancellationToken);

        using var versions = await host.VersionsAsync(cancellationToken);

        Assert.Equal(
            [JsonValueKind.Null, JsonValueKind.Null, JsonValueKind.Null],
            VersionsHost.ServiceKinds(versions));
    }

    [Fact]
    public async Task OneVersionTheServiceDidNotGiveIsNullWhileTheOthersAreAnswered()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await VersionsHost.StartAsync(new ScriptedReader(Declared("7", null, "pt-model-3.2")), cancellationToken);

        using var versions = await host.VersionsAsync(cancellationToken);

        Assert.Equal("7", versions.RootElement.GetProperty("contractVersion").GetString());
        Assert.Equal(JsonValueKind.Null, versions.RootElement.GetProperty("languageServiceVersion").ValueKind);
        Assert.Equal("pt-model-3.2", versions.RootElement.GetProperty("modelVersion").GetString());
    }

    [Fact]
    public async Task VersionsAreAskedAgainOnEveryRequest()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var reader = new ScriptedReader(
            Declared("6", "0.4.1", "pt-model-1"), ServiceVersions.Absent, Declared("7", "0.4.2", "pt-model-2"));
        await using var host = await VersionsHost.StartAsync(reader, cancellationToken);

        var answered = new List<string?[]>();
        for (var request = 0; request < 3; request++)
        {
            using var versions = await host.VersionsAsync(cancellationToken);
            answered.Add(VersionsHost.ServiceTexts(versions));
        }

        Assert.Equal(
            [["6", "0.4.1", "pt-model-1"], [null, null, null], ["7", "0.4.2", "pt-model-2"]],
            answered);
    }

    [Fact]
    public async Task OtherFieldsAndTheirOrderStayAsTheyWereWhateverTheServiceVersions()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await VersionsHost.StartAsync(new ScriptedReader(Declared("7", "0.4.2", "pt-model-3.2")), cancellationToken);

        using var versions = await host.VersionsAsync(cancellationToken);

        Assert.Equal(Fields, versions.RootElement.EnumerateObject().Select(field => field.Name));
        Assert.Equal("9.8.7", versions.RootElement.GetProperty("coreVersion").GetString());
        Assert.Equal(JsonValueKind.Null, versions.RootElement.GetProperty("schemaVersion").ValueKind);
    }

    [Fact]
    public async Task FakeRecognizerHostHasNoServiceVersionsAndNoServiceToAsk()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await VersionsHost.StartAsync(new NoServiceVersionReader(), cancellationToken);

        using var versions = await host.VersionsAsync(cancellationToken);

        Assert.Equal(
            [JsonValueKind.Null, JsonValueKind.Null, JsonValueKind.Null],
            VersionsHost.ServiceKinds(versions));
    }

    [Fact]
    public async Task SchemaVersionTheStartupConfirmedIsAnsweredAsTextOnEveryRequest()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await VersionsHost.StartAsync(new NoServiceVersionReader(), cancellationToken, VersionValue.Present("7"));

        using var first = await host.VersionsAsync(cancellationToken);
        using var second = await host.VersionsAsync(cancellationToken);

        Assert.Equal("7", first.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal("7", second.RootElement.GetProperty("schemaVersion").GetString());
    }

    [Fact]
    public async Task SchemaVersionNotConfirmedIsNullAndNotEmpty()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await VersionsHost.StartAsync(new NoServiceVersionReader(), cancellationToken);

        using var versions = await host.VersionsAsync(cancellationToken);

        Assert.Equal(JsonValueKind.Null, versions.RootElement.GetProperty("schemaVersion").ValueKind);
    }

    [Fact]
    public async Task SchemaVersionDoesNotChangeTheOtherFieldsNorTheirOrder()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await VersionsHost.StartAsync(new NoServiceVersionReader(), cancellationToken, VersionValue.Present("7"));

        using var versions = await host.VersionsAsync(cancellationToken);

        Assert.Equal(Fields, versions.RootElement.EnumerateObject().Select(field => field.Name));
        Assert.Equal("9.8.7", versions.RootElement.GetProperty("coreVersion").GetString());
        Assert.Equal(JsonValueKind.Null, versions.RootElement.GetProperty("modelVersion").ValueKind);
    }

    private static ServiceVersions Declared(string? contract, string? service, string? model) =>
        new(Version(contract), Version(service), Version(model));

    private static VersionValue Version(string? text) => text is null ? VersionValue.Absent : VersionValue.Present(text);

    /// <summary>Answers the versions it was given in order, the last one for every read after.</summary>
    private sealed class ScriptedReader(params ServiceVersions[] answers) : IServiceVersionReader
    {
        private int reads;

        public Task<ServiceVersions> ReadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(answers[Math.Min(Interlocked.Increment(ref reads), answers.Length) - 1]);
    }
}
