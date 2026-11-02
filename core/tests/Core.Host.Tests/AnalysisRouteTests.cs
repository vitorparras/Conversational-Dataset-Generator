using System.Net;
using System.Text.Json;

namespace Core.Host.Tests;

/// <summary>
/// The host analyses a text through the recognizer the configuration chose, behind the session gate: a concluded analysis answers with the count of each kind;
/// a refusal answers 503 with the reason, the cause and no analysis; a body the route does not know is refused (issue #32, CA-2 and CA-5). The host here runs in
/// process with a session seeded into its store; what the whole process writes to its log is read in Core.Tests.Integration.
/// </summary>
public sealed class AnalysisRouteTests
{
    private const string Route = "/analysis";
    private const string Marker = "ZQX-MARKER-0027-ROUTE";

    [Fact]
    public async Task HostWithTheFakeAnswersTheCountOfEachKind()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await SessionedHost.StartWithAnalysisAsync(cancellationToken);

        using var response = await host.PostAsync(Route, Body("ZQX-FAKE-PERSON falou com ZQX-FAKE-PERSON"), host.SeedSession(), cancellationToken);
        using var body = await ReadAsync(response, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, body.RootElement.GetProperty("occurrences").GetInt32());
        Assert.Equal(2, body.RootElement.GetProperty("kinds").GetProperty("PERSON").GetInt32());
        Assert.True(Guid.TryParse(body.RootElement.GetProperty("reference").GetString(), out _));
    }

    [Fact]
    public async Task RefusalAnswersServiceUnavailableWithTheReasonTheCauseAndNoAnalysis()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await SessionedHost.StartWithAnalysisAsync(cancellationToken);

        using var response = await host.PostAsync(Route, Body(Marker), host.SeedSession(), cancellationToken);
        using var body = await ReadAsync(response, cancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("ConnectionRefused", body.RootElement.GetProperty("reason").GetString());
        Assert.Equal(
            "Material não analisado e nada gravado: o serviço de linguagem não pôde ser alcançado.",
            body.RootElement.GetProperty("cause").GetString());
        Assert.False(body.RootElement.TryGetProperty("occurrences", out _));
        Assert.False(body.RootElement.TryGetProperty("kinds", out _));
    }

    [Theory]
    [InlineData(/*lang=json,strict*/ """{"text":"ZQX-FAKE-NOTHING","language":"pt","resume":"00000000-0000-0000-0000-000000000027"}""")]
    [InlineData(/*lang=json,strict*/ """{"language":"pt"}""")]
    [InlineData(/*lang=json,strict*/ """{"text":null,"language":"pt"}""")]
    [InlineData(/*lang=json,strict*/ """{"text":"ZQX-FAKE-NOTHING","language":"pt","resumes":"ZQX-NOT-A-REFERENCE"}""")]
    public async Task BodyOutsideTheShapeOfTheRouteIsRefusedWithoutAnalysis(string json)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await SessionedHost.StartWithAnalysisAsync(cancellationToken);

        using var response = await host.PostAsync(Route, json, host.SeedSession(), cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RequestWithNoSessionIsNotAnalysed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await SessionedHost.StartWithAnalysisAsync(cancellationToken);

        using var response = await host.PostAsync(Route, Body("ZQX-FAKE-PERSON"), identifier: null, cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string Body(string text) =>
        JsonSerializer.Serialize(new Dictionary<string, string?> { ["text"] = text, ["language"] = "pt", ["resumes"] = null });

    private static async Task<JsonDocument> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
}
