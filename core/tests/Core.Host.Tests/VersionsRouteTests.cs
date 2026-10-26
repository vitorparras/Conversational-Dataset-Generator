using System.Text.Json;
using Core.Boundary.Fake;
using Core.Boundary.Http;
using Core.Platform.Versions;

namespace Core.Host.Tests;

/// <summary>The versions route asks for the model version on each request and answers it beside the fields that do not change.</summary>
public sealed class VersionsRouteTests
{
    private static readonly string[] Fields =
        ["coreVersion", "commit", "workingTree", "contractVersion", "languageServiceVersion", "modelVersion", "schemaVersion"];

    [Fact]
    public async Task ModelVersionTheServiceGaveIsAnswered()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await VersionsHost.StartAsync(new ScriptedReader(VersionValue.Present("pt-model-3.2")), cancellationToken);

        using var versions = await host.VersionsAsync(cancellationToken);

        Assert.Equal("pt-model-3.2", versions.RootElement.GetProperty("modelVersion").GetString());
    }

    [Fact]
    public async Task ModelVersionTheServiceDidNotGiveIsNullAndNotEmpty()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await VersionsHost.StartAsync(new ScriptedReader(VersionValue.Absent), cancellationToken);

        using var versions = await host.VersionsAsync(cancellationToken);

        Assert.Equal(JsonValueKind.Null, versions.RootElement.GetProperty("modelVersion").ValueKind);
    }

    [Fact]
    public async Task ModelVersionIsAskedAgainOnEveryRequest()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var reader = new ScriptedReader(VersionValue.Present("pt-model-1"), VersionValue.Absent, VersionValue.Present("pt-model-2"));
        await using var host = await VersionsHost.StartAsync(reader, cancellationToken);

        var answered = new List<string?>();
        for (var request = 0; request < 3; request++)
        {
            using var versions = await host.VersionsAsync(cancellationToken);
            answered.Add(versions.RootElement.GetProperty("modelVersion").GetString());
        }

        Assert.Equal(["pt-model-1", null, "pt-model-2"], answered);
    }

    [Fact]
    public async Task OtherFieldsAndTheirOrderStayAsTheyWereWhateverTheModelVersion()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await VersionsHost.StartAsync(new ScriptedReader(VersionValue.Present("pt-model-3.2")), cancellationToken);

        using var versions = await host.VersionsAsync(cancellationToken);

        Assert.Equal(Fields, versions.RootElement.EnumerateObject().Select(field => field.Name));
        Assert.Equal("9.8.7", versions.RootElement.GetProperty("coreVersion").GetString());
        Assert.Equal(JsonValueKind.Null, versions.RootElement.GetProperty("contractVersion").ValueKind);
    }

    [Fact]
    public async Task FakeRecognizerHostHasNoModelVersionAndNoServiceToAsk()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await VersionsHost.StartAsync(new NoModelVersionReader(), cancellationToken);

        using var versions = await host.VersionsAsync(cancellationToken);

        Assert.Equal(JsonValueKind.Null, versions.RootElement.GetProperty("modelVersion").ValueKind);
    }

    /// <summary>Answers the versions it was given in order, the last one for every read after.</summary>
    private sealed class ScriptedReader(params VersionValue[] answers) : IModelVersionReader
    {
        private int reads;

        public Task<VersionValue> ReadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(answers[Math.Min(Interlocked.Increment(ref reads), answers.Length) - 1]);
    }
}
